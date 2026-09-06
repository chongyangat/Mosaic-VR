# Publish and Update with GitHub Desktop

This directory is an independent local Git repository prepared for the public source snapshot. Its configured remote is `https://github.com/chongyangat/VBSOED-paper-source.git`.

## Update the existing GitHub repository

1. Open GitHub Desktop and select **File → Add local repository** if the repository is not already listed.
2. Choose `E:\VRfiles\VBSOED-paper-source-159447c`, then select **Add repository**.
3. Confirm that the current branch is `main`.
4. Select **Fetch origin**, then pull first if GitHub Desktop reports incoming commits. Resolve any conflict before continuing.
5. Review the **Changes** list. For this documentation update, it should include:
   - the English Markdown files;
   - `.gitignore`;
   - ten PNG files under `docs/images/workflow/`.
6. Confirm that `source_675a9d1e3b/` and `SUBFIGURE_MAP.json` do **not** appear in the change list. That working directory contains duplicate images and a machine-specific mapping file and is intentionally ignored.
7. Preview `README.md` or review its diff, paying particular attention to the Quest and PC image order.
8. Enter a commit summary such as `docs: add English workflow guide and screenshots`.
9. Select **Commit to main**, then **Push origin**.

## Publish for the first time

Use these steps only if the remote repository has not yet been created:

1. Sign in to the intended GitHub account in GitHub Desktop.
2. Add `E:\VRfiles\VBSOED-paper-source-159447c` as a local repository.
3. Select **Publish repository**.
4. Use a repository name such as `VBSOED-paper-source` and add the paper-related description.
5. Clear **Keep this code private** only when public release has been approved, and select the correct personal account or organization.
6. Select **Publish Repository**.

## Post-upload checks

- The repository home page renders `README.md` and all ten workflow images.
- The Quest gallery order is safety/start → shopping list → supermarket task → completion summary.
- The PC gallery follows the requested order: participant profile → operating mode → scene list → active scene selection → recording dashboard → completion summary.
- `source_675a9d1e3b/`, `SUBFIGURE_MAP.json`, `Assets/Arts/`, `Assets/AssetRaw/`, `Assets/Proxima/`, `Assets/Mirror/`, and `Packages/LocalPackages/` are absent from GitHub.
- No certificates, signing keys, native binaries, archives, APKs, logs, or personal research data were uploaded.
- Add the paper title, authors, DOI, citation metadata, and a license only after the project team has approved their exact wording.

GitHub documentation: [Add a repository from your local computer](https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-a-repository-from-your-local-computer-to-github-desktop) and [add an existing project with GitHub Desktop](https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-an-existing-project-to-github-using-github-desktop).
