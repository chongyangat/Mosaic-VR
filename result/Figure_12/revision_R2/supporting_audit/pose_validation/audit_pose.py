"""Read-only audit of raw recordings, rerun results, and source provenance."""
from pathlib import Path
import hashlib, json, platform, subprocess, sys
import numpy as np
import pandas as pd
import scipy
from scipy.spatial.transform import Rotation
from run_original_analysis import BASE, SOURCE, RAW, module as a

R = json.loads((BASE/'rerun/analysis_results.json').read_text(encoding='utf-8'))
O = json.loads((SOURCE/'05_Data/quest_mocap_validation/processed/analysis_results.json').read_text(encoding='utf-8'))
q = pd.read_csv(RAW/'QuestPose_20260829_173101.csv')
m = pd.read_csv(RAW/'update_timestamp1-Tracker0.csv', skiprows=11, skipinitialspace=True)
req = ['PredictedDisplayTime代理-QuestUnix估算(ms)','时钟偏移(ms)'] + ['RenderPredicted-TrackingSpace位置'+x for x in 'XYZ'] + ['RenderPredicted-TrackingSpace四元数'+x for x in 'XYZW']
sync = q['时钟同步有效'].astype(str).str.lower().eq('true')
render = q['RenderPoseTime有效'].astype(str).str.lower().eq('true')
base = sync & render & q[req].notna().all(axis=1)
delay = q['PoseReadTime至PC观测延迟(ms)'].le(60)
horizon = q['PredictionHorizon(ms)'].le(70)
unc = q['偏移不确定度上界(ms)'].le(3)
quality = base & delay & horizon & unc
tq = q['PredictedDisplayTime代理-QuestUnix估算(ms)']-q['时钟偏移(ms)']
qm = R['data_quality']['quest']
selected = quality & tq.between(qm['selected_segment_start_ms'], qm['selected_segment_end_ms'])
attr=[]
last=len(q)
for stage,mask in [('raw',np.ones(len(q),bool)),('sync_valid_and_render_time_valid_and_required_fields',base),('observation_delay_le60ms',base&delay),('prediction_horizon_le70ms',base&delay&horizon),('offset_uncertainty_le3ms',quality),('unique_valid_pose_time',quality),('most_rows_continuous_segment',selected)]:
    n=int(np.sum(mask));attr.append(dict(stream='Quest',stage=stage,retained_rows=n,removed_from_previous=last-n));last=n
mr=['Timestamp','XToGlobal1','YToGlobal1','ZToGlobal1','QxToGlobal1','QyToGlobal1','QzToGlobal1','QwToGlobal1']
mv=m['Timestamp'].gt(0)&m[mr].notna().all(axis=1)
mm=R['data_quality']['mocap']
ms=mv&m['Timestamp'].between(mm['selected_segment_start_ms'], mm['selected_segment_end_ms'])
last=len(m)
for stage,mask in [('raw',np.ones(len(m),bool)),('positive_timestamp_and_required_fields',mv),('most_rows_continuous_segment',ms)]:
    n=int(np.sum(mask));attr.append(dict(stream='MoCap',stage=stage,retained_rows=n,removed_from_previous=last-n));last=n
pd.DataFrame(attr).to_csv(BASE/'row_attrition.csv',index=False,encoding='utf-8-sig')
stats=[]
for pop,mask in [('all_base_valid',base),('all_quality',quality),('selected_segment',selected)]:
    for c in ['PredictionHorizon(ms)','PoseReadTime至PC观测延迟(ms)','偏移不确定度上界(ms)','同步RTT(ms)','同步年龄(ms)','时钟偏移(ms)']:
        v=q.loc[mask,c]
        stats.append(dict(population=pop,field=c,n=int(v.notna().sum()),minimum=v.min(),median=v.median(),p95=v.quantile(.95),maximum=v.max(),negative_count=int((v<0).sum())))
pd.DataFrame(stats).to_csv(BASE/'quality_statistics_by_population.csv',index=False,encoding='utf-8-sig')
rows=q[['序号','Quest位姿序号','QuestUnity帧','Quest采样阶段','时钟同步状态']].copy()
rows['mapped_prediction_time_pc_ms']=tq
rows['base_valid']=base;rows['delay_pass']=delay;rows['horizon_pass']=horizon;rows['uncertainty_pass']=unc;rows['quality_pass']=quality;rows['selected_segment']=selected
rows.to_csv(BASE/'quest_row_decisions.csv',index=False,encoding='utf-8-sig')

compare=[]
def cmp(x,y,path=''):
    if isinstance(x,dict) and isinstance(y,dict):
        for k in x.keys()&y.keys(): cmp(x[k],y[k],path+'.'+k)
    elif isinstance(x,list) and isinstance(y,list):
        for i,(u,v) in enumerate(zip(x,y)): cmp(u,v,path+f'[{i}]')
    elif isinstance(x,(int,float)) and not isinstance(x,bool) and isinstance(y,(int,float)):
        compare.append(dict(field=path.lstrip('.'),original=y,rerun=x,difference=x-y))
cmp(R,O)
pd.DataFrame(compare).to_csv(BASE/'original_vs_rerun_numeric.csv',index=False)

mocap,_,_=a.read_mocap(RAW/'update_timestamp1-Tracker0.csv')
quest,_,_=a.read_quest(RAW/'QuestPose_20260829_173101.csv')
over=R['data_quality'];t0=over['analysis_overlap_start_ms'];t1=over['analysis_overlap_end_ms'];split=over['calibration_end_ms']
grid=np.arange(t0,t1,a.DENSE_DT_MS)
X=np.array(R['spatial_registration']['TrackerFromCenterEye']);Y=np.array(R['spatial_registration']['MocapUnityFromQuest'])
lever=np.linalg.norm(X[:3,3])
# Post-hoc sensitivity only: use the already calibration-fitted rigid lever arm.
# This is NOT an independently validated replacement primary estimator.
eye=a.PoseSeries(mocap.time_ms, mocap.position_m+mocap.rotation.apply(X[:3,3]), mocap.rotation*Rotation.from_matrix(X[:3,:3]))
speed,ang=a.dense_kinematic_signals(eye,grid)
qs,qa=a.dense_kinematic_signals(quest,grid)
lags=np.arange(a.LAG_SEARCH_MIN_MS,a.LAG_SEARCH_MAX_MS+a.LAG_SEARCH_STEP_MS*.5,a.LAG_SEARCH_STEP_MS)
_,wt,_=a.select_time_window(grid,speed,ang,qs,qa,t0+500,split-500,lags)
wt.to_csv(BASE/'leverarm_corrected_timing_sensitivity.csv',index=False)
original_wt=pd.read_csv(BASE/'rerun/cross_correlation_windows.csv')
sens={}
for label,w in [('original_tracker_origin',original_wt),('posthoc_calibrated_eye_origin',wt)]:
    sens[label]={c:float(w[c].median()) for c in ['best_lag_combined_ms','best_lag_position_ms','best_lag_rotation_ms']}

repo=Path(r'D:\vrcontent\MOSAIC-VR\VR_project\VBSOED');commit='159447c26485714716aa38c5353bf4c158d770aa'
def git(*args):
    p=subprocess.run(['git','-C',str(repo),*args],capture_output=True,text=True,encoding='utf-8',errors='replace');return dict(returncode=p.returncode,stdout=p.stdout.strip(),stderr=p.stderr.strip())
provenance={'specified_commit':git('show','-s','--format=%H%n%aI%n%s',commit), 'HEAD':git('rev-parse','HEAD')}
for name in ['Assets/GameScripts/Runtime/Recording/QuestPoseRecorder.cs','Assets/GameScripts/Runtime/Characters/Player/VirtualPlayerControllor.cs']:
    latest=git('log','-1','--format=%H%n%aI%n%s','--',name)
    prescribed=git('show',f'{commit}:{name}')
    stem=Path(name).stem
    if prescribed['returncode']==0: (BASE/'source_snapshot'/f'{stem}_159447c.cs').write_text(prescribed['stdout'],encoding='utf-8')
    past=git('show',f'71ff03c11f1669a0b15fa42fa47c0109770a82fb:{name}')
    if past['returncode']==0: (BASE/'source_snapshot'/f'{stem}_71ff03c.cs').write_text(past['stdout'],encoding='utf-8')
    provenance[name]={'latest_file_commit':latest,'working_tree_diff':git('status','--short','--',name),'exists_in_specified_commit':prescribed['returncode']==0,'current_sha256':hashlib.sha256((repo/name).read_bytes()).hexdigest()}

identity=q['PredictedDisplayTime代理-QuestUnix估算(ms)']-(q['PoseReadTime-QuestUnix(ms)']+(q['PredictedDisplayTime代理-OVR单调时钟(s)']-q['PoseReadTime-OVR单调时钟(s)'])*1000)
readmap=q['PoseReadTime-PC时钟域(ms)']-(q['PoseReadTime-QuestUnix(ms)']-q['时钟偏移(ms)'])
obs=q['PoseReadTime至PC观测延迟(ms)']-(q['PC时间戳(ms)']-q['PoseReadTime-PC时钟域(ms)'])
quat=q.loc[base,['RenderPredicted-TrackingSpace四元数'+x for x in 'XYZW']].to_numpy()
facts={
 'runtime':{'executable':sys.executable,'python':sys.version,'platform':platform.platform(),'numpy':np.__version__,'pandas':pd.__version__,'scipy':scipy.__version__,'bundled_python_failed':'ModuleNotFoundError: matplotlib'},
 'row_attrition':attr,'quality_stats':stats,
 'quality_fail_counts_independent':{'observation_delay':int((base&~delay).sum()),'prediction_horizon':int((base&~horizon).sum()),'offset_uncertainty':int((base&~unc).sum()),'any':int((base&~quality).sum()),'sync_boolean_vs_ready_state_mismatch':int((sync!=q['时钟同步状态'].eq('Ready')).sum())},
 'raw_checks':{'mocap_zero_timestamp':int(m.Timestamp.eq(0).sum()),'mocap_valid_nonincreasing_timestamp_count':int((np.diff(m.loc[mv,'Timestamp'])<=0).sum()),'quest_valid_zero_norm_quaternions':int((np.linalg.norm(quat,axis=1)<=1e-10).sum()),'quest_quality_duplicate_time_count':int(tq.loc[quality].duplicated().sum()),'quest_all_sample_phase_counts':q['Quest采样阶段'].value_counts().to_dict(),'quest_selected_sample_phase_counts':q.loc[selected,'Quest采样阶段'].value_counts().to_dict(),'raw_pose_valid_count':int(q['RawPose有效'].astype(str).str.lower().eq('true').sum()),'selected_unique_sequence':int(q.loc[selected,'Quest位姿序号'].nunique()),'selected_unique_unity_frame':int(q.loc[selected,'QuestUnity帧'].nunique()),'base_span_seconds':float((tq[base].max()-tq[base].min())/1000),'quality_span_seconds':float((tq[quality].max()-tq[quality].min())/1000),'mocap_selected_effective_rate_hz':float((ms.sum()-1)/(m.loc[ms,'Timestamp'].max()-m.loc[ms,'Timestamp'].min())*1000),'quest_selected_effective_rate_hz':float((selected.sum()-1)/(tq[selected].max()-tq[selected].min())*1000)},
 'time_arithmetic_max_abs_error_ms':{'predicted_unix_proxy':float(identity.abs().max()),'read_time_mapping':float(readmap.abs().max()),'observation_delay':float(obs.abs().max())},
 'numeric_comparison':{'fields_compared':len(compare),'maximum_abs_difference':max(abs(x['difference']) for x in compare),'fields_differing_gt_1e_8':sum(abs(x['difference'])>1e-8 for x in compare)},
 'fitted_tracker_to_eye_translation_m':X[:3,3].tolist(),'fitted_leverarm_mm':float(lever*1000),
 'timing_sensitivity':sens,'source_provenance':provenance
}
(BASE/'audit_facts.json').write_text(json.dumps(facts,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in facts.items() if k not in ['quality_stats','source_provenance']},ensure_ascii=False,indent=2))
