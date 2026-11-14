# CI runner on Ubuntu

`.github/workflows/build-server-linux.yml` builds the Linux dedicated server with Unity and pushes the Docker image from a
self-hosted **Ubuntu** runner. It replaces the Windows runner (`build-server.yml`) when the repository variable
`CI_RUNNER` is not `windows` (the Linux workflow is the default). Reasons to move: Docker runs natively on Ubuntu (no Docker Desktop / WSL engine that hangs when the PC is out
of memory) and the build machine is not the machine you work on.

## What the runner PC needs

| | |
|---|---|
| OS | Ubuntu **22.04 LTS** (20.04 also works; Unity 2023.1 does not officially support 24.04) |
| RAM | 16 GB or more, plus 8 GB swap (importing this HDRP project is heavy) |
| Disk | 80 GB free (Unity Library about 13 GB, project 8 GB, map 4 GB, Docker images) |
| Network | reaches github.com and `registry.kushanherath.me` |

The first run imports the whole project and takes 30 to 90 minutes. Later runs reuse the `Library` folder of the runner workspace
and take a few minutes.

## 1. Packages

```bash
sudo apt update
sudo apt install -y git git-lfs rsync curl ca-certificates docker.io
git lfs install
sudo usermod -aG docker "$USER"      # the runner user may use Docker; log out and in again afterwards
```

## 2. Unity 2023.1.22f1 with Linux Dedicated Server support

```bash
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg >/dev/null
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'
sudo apt update && sudo apt install -y unityhub
```

Install the editor (the version and changeset come from `ProjectSettings/ProjectVersion.txt`):

```bash
unityhub -- --headless install --version 2023.1.22f1 --changeset c1416845ad01 --module linux-server
```

If the headless install is not available on your Hub version, open Unity Hub, Installs, Install Editor, pick 2023.1.22f1 and tick
**Linux Dedicated Server Build Support**. The editor binary ends up at
`~/Unity/Hub/Editor/2023.1.22f1/Editor/Unity` (the workflow's default; another place goes into the variable `UNITY_PATH`).

**License.** Open Unity Hub once on this PC (it needs a desktop session, or a monitor for the first time), sign in, and add the free
**Personal** license. The runner must run as the **same user**, because the license is stored per user.

## 3. Map assets (git-ignored, 4 GB)

`Assets/TerrainDemoScene_HDRP` is not in git. Copy it once from your Windows PC, together with its `.meta` file:

```bash
# on the Ubuntu PC (replace WINDOWS-PC with its address, or use a USB stick)
mkdir -p ~/railbound-map-assets
scp -r "kushan@WINDOWS-PC:C:/Users/kushan/Railbound/Assets/TerrainDemoScene_HDRP" ~/railbound-map-assets/
scp "kushan@WINDOWS-PC:C:/Users/kushan/Railbound/Assets/TerrainDemoScene_HDRP.meta" ~/railbound-map-assets/
test -f ~/railbound-map-assets/TerrainDemoScene_HDRP/Scenes/MAP-2.unity && echo "map OK"
```

Another folder: set the repository variable `MAP_ASSETS_DIR` to it. The `.meta` must sit next to the folder (same name plus `.meta`).
When the map changes on your Windows PC, copy it again.

## 4. The runner

Repository, Settings, Actions, Runners, **New self-hosted runner**, Linux x64. GitHub shows the download commands and a one-time
token (keep it private). Configure it with the label `unity`:

```bash
mkdir -p ~/actions-runner && cd ~/actions-runner
# ... download and extract exactly as GitHub shows ...
./config.sh --url https://github.com/OneEngine-studio/Railbound --token <TOKEN-FROM-GITHUB> --labels unity --name ubuntu-unity
sudo ./svc.sh install "$USER"
sudo ./svc.sh start
sudo ./svc.sh status
```

It starts at boot and restarts by itself. The workflow selects it with `runs-on: [self-hosted, linux]` (the label `unity` is optional).

## 5. Which runner builds

The Linux workflow is the default. Repository variables (Settings, Secrets and variables, Actions, Variables):

| variable | value |
|---|---|
| `CI_RUNNER` | leave unset (Linux). `windows` switches back to the Windows runner |
| `DEPLOY_GITOPS` | `true` (already set if the Windows flow bumped the image tag) |
| `UNITY_PATH`, `MAP_ASSETS_DIR` | only if you used other locations |

The secrets `REGISTRY_USERNAME`, `REGISTRY_PASSWORD` (and `GITOPS_TOKEN`) are the ones the Windows workflow already uses.
Stop the Windows runner service so the two never compete for a job.

## Arch Linux notes

The same workflow runs on Arch. Differences from the Ubuntu steps above:

```bash
sudo pacman -S --needed git git-lfs rsync docker procps-ng coreutils
sudo systemctl enable --now docker
sudo usermod -aG docker "$USER"            # log out and in again
git lfs install
yay -S unityhub                             # then install 2023.1.22f1 + Linux Dedicated Server Support from the Hub
```

- Unity path: the default `~/Unity/Hub/Editor/2023.1.22f1/Editor/Unity` is where the Hub installs; check with `ls ~/Unity/Hub/Editor`.
- License on a server without a desktop: use the manual flow. Create the activation file
  `Unity -batchmode -nographics -createManualActivationFile`, upload the `.alf` at license.unity3d.com (Personal), save the returned
  `.ulf` and run `Unity -batchmode -nographics -manualLicenseFile <file>.ulf` as the runner user.
- Run the runner as a service (`sudo ./svc.sh install "$USER" && sudo ./svc.sh start`) instead of `./run.sh`, so it survives logout.

## 6. First run

Actions, **Build & push dedicated server (Linux runner)**, Run workflow (or `gh workflow run build-server-linux.yml`). Watch the step
*Build Linux dedicated server*: the log should contain `Server shader stripping removed N shader variants`.

## Problems

| message | cause and fix |
|---|---|
| `Unity editor not found or not executable` | wrong path: install the editor or set `UNITY_PATH` |
| `No valid Unity Editor license found` in `unity-import.log` | the Hub was never signed in as the runner's user: sign in there, or run the service as the user who did |
| `Map assets not found at ...` | step 3 (folder, or the variable `MAP_ASSETS_DIR`) |
| `Docker engine does not answer` | `sudo systemctl status docker`; the runner user must be in the `docker` group (restart the runner service after `usermod`) |
| import takes very long | normal the first time; the `Library` folder is kept for the next runs |
| a build stops after 90 minutes | the Unity step has a hard limit (a hung editor is killed); see `unity-build.log` in the uploaded artifact |

## Security

* Use a **private** repository with this runner: a self-hosted runner runs the code of every workflow on that machine.
* Docker access is root-equivalent on the runner PC: use a machine that only does this job.
* The registry password must be long and random (store it only as the secret `REGISTRY_PASSWORD`).
