# GitHub Actions 工作流程说明

## 概述

本仓库配置了自动构建和发布工作流程，可以在推送代码或手动触发时自动构建Unity游戏的可执行文件并创建发布版本。

## 工作流程：Build and Release

### 触发条件

工作流程会在以下情况下自动运行：

1. **推送到主分支**：当代码推送到 `main` 或 `master` 分支时
2. **推送标签**：当推送以 `v` 开头的标签时（例如：`v1.0.0`）
3. **手动触发**：通过 GitHub Actions 界面手动运行

### 构建平台

工作流程会自动为以下平台构建游戏：

- Windows 64位 (StandaloneWindows64)
- Linux 64位 (StandaloneLinux64)
- macOS (StandaloneOSX)

### 必需的配置

在使用此工作流程之前，需要在仓库中设置以下 Secrets：

1. **UNITY_LICENSE**：Unity 许可证文件内容
2. **UNITY_EMAIL**：Unity 账户邮箱
3. **UNITY_PASSWORD**：Unity 账户密码

#### 如何获取 Unity License

1. 在本地安装 Unity Hub 和对应版本的 Unity 编辑器（2022.3.62f2c1）
2. 运行以下命令获取许可证文件：

```bash
# 激活 Unity 许可证
unity-editor -batchmode -createManualActivationFile -logFile

# 会生成一个 .alf 文件，在 Unity 网站上传此文件以获取许可证
```

3. 将获取的许可证文件内容复制到 GitHub Secrets 中的 `UNITY_LICENSE`

#### 设置 GitHub Secrets

1. 进入仓库设置 → Secrets and variables → Actions
2. 点击 "New repository secret"
3. 添加以下三个 secrets：
   - `UNITY_LICENSE`
   - `UNITY_EMAIL`
   - `UNITY_PASSWORD`

### 使用方法

#### 方法一：通过标签创建发布版本

```bash
# 创建标签
git tag v1.0.0

# 推送标签到远程仓库
git push origin v1.0.0
```

这将触发工作流程，构建所有平台的可执行文件，并创建名为 `v1.0.0` 的 Release。

#### 方法二：手动触发

1. 进入 GitHub 仓库页面
2. 点击 "Actions" 标签
3. 选择 "Build and Release" 工作流程
4. 点击 "Run workflow" 按钮
5. （可选）输入版本号，如 `v1.0.1`
6. 点击 "Run workflow" 确认

#### 方法三：推送到主分支

直接推送代码到 `main` 或 `master` 分支将触发构建，但只有在手动触发或推送标签时才会创建 Release。

### 输出

工作流程完成后会产生以下输出：

1. **构建产物（Artifacts）**：每个平台的构建文件会作为 Artifacts 上传，保留 7 天
2. **GitHub Release**：如果是通过标签或手动触发，会创建一个包含以下文件的 Release：
   - `AstroShooter_Beta-Windows.zip`
   - `AstroShooter_Beta-Linux.zip`
   - `AstroShooter_Beta-macOS.zip`

### 工作流程详情

工作流程包含两个主要作业：

1. **build**：并行构建三个平台的可执行文件
   - 使用 Unity Builder 进行构建
   - 缓存 Unity Library 以加快构建速度
   - 上传构建产物

2. **create-release**：创建 GitHub Release
   - 下载所有构建产物
   - 将每个平台的构建文件打包成 zip
   - 创建 Release 并上传 zip 文件

### 注意事项

- Unity 版本设置为 `2022.3.62f2c1`，与项目的 Unity 版本匹配
- 构建过程可能需要较长时间（每个平台约 10-30 分钟）
- 确保仓库有足够的 GitHub Actions 运行时间配额
- 如果构建失败，请检查 Unity License 配置和项目设置

### 故障排除

如果工作流程失败，请检查：

1. Unity License 是否正确配置
2. Unity Email 和 Password 是否正确
3. 项目是否有编译错误
4. GitHub Actions 日志中的详细错误信息

### 自定义

如需修改构建配置，可以编辑 `.github/workflows/build-and-release.yml` 文件：

- 修改 `UNITY_VERSION` 以匹配不同的 Unity 版本
- 添加或删除 `targetPlatform` 以支持不同的平台
- 修改 `buildName` 以更改可执行文件名称
