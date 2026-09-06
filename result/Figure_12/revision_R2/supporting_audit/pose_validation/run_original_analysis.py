from pathlib import Path
import hashlib, importlib.util, json, shutil, sys
sys.dont_write_bytecode = True
BASE = Path(__file__).resolve().parent
SOURCE = Path(r'C:\Users\zhouc\Nutstore\2\VR工作站 (2)\MOSAIC-VR\project_mosaic_vr\submission')
SCRIPT = SOURCE / '06_Code/quest_mocap_validation/analyze_update_timestamp.py'
RAW = SOURCE / '05_Data/quest_mocap_validation/raw'
(BASE / 'source_snapshot').mkdir(parents=True, exist_ok=True)
COPY = BASE / 'source_snapshot/analyze_update_timestamp.py'
shutil.copy2(SCRIPT, COPY)
spec = importlib.util.spec_from_file_location('pose_analysis_original', COPY)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
module.MOCAP_FILE = RAW / 'update_timestamp1-Tracker0.csv'
module.QUEST_FILE = RAW / 'QuestPose_20260829_173101.csv'
module.OUTPUT_DIR = BASE / 'rerun'
module.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
manifest = {str(p): {'sha256': hashlib.sha256(p.read_bytes()).hexdigest(), 'bytes': p.stat().st_size} for p in [SCRIPT, module.MOCAP_FILE, module.QUEST_FILE]}
(BASE/'input_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
if __name__ == '__main__':
    module.main()
