# 使用 GitHub Desktop 发布

此目录已经准备为独立的本地 Git 仓库，不包含原始仓库远端，也不会上传原始仓库后续提交或未提交修改。

## 发布步骤

1. 打开 GitHub Desktop，并登录要发布到的 GitHub 账号。
2. 选择 **File → Add local repository**。
3. 点击 **Choose...**，选择：

   `E:\VRfiles\VBSOED-paper-source-159447c`

4. 点击 **Add repository**。
5. 确认 GitHub Desktop 显示 `main` 分支，且 Changes 页面没有待提交文件。
6. 点击顶部的 **Publish repository**。
7. 建议仓库名称使用 `VBSOED-paper-source`，并填写论文相关描述。
8. 需要公开时，取消勾选 **Keep this code private**；选择正确的个人账号或 Organization。
9. 点击 **Publish Repository**。

## 发布后检查

- GitHub 首页能正常显示 `README.md`。
- 最新提交信息为 `chore: prepare public paper source snapshot`。
- 仓库中不存在 `Assets/Arts`、`Assets/AssetRaw`、`Assets/Proxima`、`Assets/Mirror` 或 `Packages/LocalPackages`。
- 仓库中不存在 `.pfx`、`.dll`、`.exe`、模型、纹理、音视频或压缩包。
- 根据团队决定补充 LICENSE、论文标题、作者、DOI 和推荐引用格式。

GitHub 官方说明：[添加本地仓库](https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-a-repository-from-your-local-computer-to-github-desktop)；[使用 GitHub Desktop 发布现有项目](https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-an-existing-project-to-github-using-github-desktop)。

