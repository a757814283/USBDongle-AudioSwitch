# USB 无线耳机音频自动切换

**[English documentation → README.md](README.md)**

这是一个常驻托盘的 Windows 小工具：监听无线耳机 USB 接收器（Dongle）发出的 HID 状态报告，据此切换系统默认播放设备 —— 耳机**开机**时切到耳机，**关机**时切回扬声器，全程无需手动操作。

本项目是原 `audio_switch.ps1` 的 C# WinForms 重写版：不再有常驻的 PowerShell 进程，不依赖 `Get-PnpDevice`，也不需要外部 `SoundVolumeView.exe`。它直接调用 IPolicyConfig，每次打开设备前都重新解析接收器的设备路径，因此换到别的 USB 口也能照常工作；界面提供带实时签名/校验和反馈的设备选择，安装时以当前用户身份进行、不弹 UAC。

> 本项目受 [Meladon90/Audio-Switch](https://github.com/Meladon90/Audio-Switch) 启发。

<img width="1232" height="807" alt="image" src="https://github.com/user-attachments/assets/0f00272b-0770-44b9-aa2f-63ba20b7ad89" />

---
## 已测试设备：
1. Alienware Pro Headset

## 目录

- [工作原理](#工作原理)
- [系统要求](#系统要求)
- [构建](#构建)
- [界面说明](#界面说明)
- [配置文件](#配置文件)
- [文件位置](#文件位置)
- [开机自启动](#开机自启动)
- [HID 签名与校验和](#hid-签名与校验和)
- [故障排查](#故障排查)
- [项目结构](#项目结构)

---

## 工作原理

```
USB Dongle ──HID 输入报告──▶ HidMonitor ──匹配签名──▶ MonitorController
                                                              │
                                              ┌───────────────┴───────────────┐
                                         开机签名                        关机签名
                                              │                               │
                                     切换到耳机端点                  切换到扬声器端点
                                              └───────────▶ AudioSwitcher ◀───┘
                                                     （IPolicyConfig COM）
```

1. **定位设备** — `HidDeviceLocator` 用 SetupAPI 枚举当前存在的 HID 集合，取实例 ID 中包含匹配串（默认 `VID_413C&PID_A529&MI_05&COL02`）的第一个。设备路径在每次打开时**重新解析**，因此换 USB 口、拔插都能自愈，不需要改配置。
2. **读取报告** — `HidMonitor` 用重叠 I/O（`FILE_FLAG_OVERLAPPED`）阻塞式读取输入报告，缓冲区 256 字节。
3. **匹配签名** — 报告的前 `signatureLength`（默认 8）个字节与开机/关机签名逐字节精确比对。**先测关机签名，再测开机签名**。命中后进入冷却期（默认 1500 ms），把开机时的一串突发报告收敛成一次切换。
4. **切换设备** — `AudioSwitcher` 直接调用未公开但自 Win7 起稳定的 `IPolicyConfig` COM 接口（`CPolicyConfigClient`），也就是 `SoundVolumeView.exe` 内部用的同一个接口。默认同时设置 **Console / Multimedia / Communications** 三个角色，与原脚本一致。
5. **失败回退** — 若 COM 调用失败，且配置里填了 `soundVolumeViewPath`，则回退到调用 `SoundVolumeView.exe /SetDefault`。

切换前会**实时读取**当前默认设备并比对，已是目标设备就跳过，不会重复设置。

---

## 系统要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 7 及以上（64 位 Windows 10/11 已实测） |
| 运行时 | .NET Framework **4.8** |
| 构建 | MSBuild（Visual Studio、Build Tools 或 JetBrains Rider 自带的均可） |
| 可选 | `SoundVolumeView.exe`（仅当原生 COM 调用被拦截时需要） |

.NET Framework 4.8 随 Windows 10（1903 及以上）与 Windows 11 一同提供，因此在新系统上无需额外安装。较老的系统可从 [.NET Framework 4.8 运行时下载页](https://dotnet.microsoft.com/download/dotnet-framework/net48)获取。

> **本程序不使用 .NET 8、.NET 6 或任何 .NET Core / .NET 5+ 运行时。**
> 它是一个 .NET Framework 4.8 程序，`USBDongle_AudioSwitch.exe.config` 里也是这样声明的。如果登录时 Windows 弹出要求安装「.NET 桌面运行时」的提示，那个提示来自机器上的**其他程序**——装 .NET 8 运行时不会改变本程序的任何行为。

.NET Framework 4.8 **目标包（Targeting Pack）不是必需的** —— `build.ps1` 在缺失时会自动改用运行时程序集编译，详见[构建](#构建)。

---

## 构建

```powershell
cd USBDongle_AudioSwitch
powershell -File build.ps1
```

产物：`bin\Release\USBDongle_AudioSwitch.exe`

### 参数

| 参数 | 说明 |
| --- | --- |
| `-Configuration Debug` | 生成 Debug 版本（默认 `Release`） |
| `-MSBuildPath <路径>` | 显式指定 `MSBuild.exe`，跳过自动查找 |
| `-NoFrameworkPathOverride` | 目标包缺失时**不**回退，直接按原样编译（用于暴露真实的环境问题） |

### 单实例

程序用 `Local\USBDongle_AudioSwitch.SingleInstance` 互斥量保证只有一个实例。重复启动不会开启第二个进程，而是**激活已有实例的主窗口**（通过 `Local\USBDongle_AudioSwitch.Activate` 命名事件通知，后台线程负责接收）。

### 托盘行为

- **关闭主窗口** = 最小化到托盘，监控继续运行（首次会弹气泡提示）。
- 真正退出只能通过**托盘右键 → 退出**。
- 双击托盘图标可重新打开主窗口。

---

## 界面说明

### 音频设备

| 控件 | 说明 |
| --- | --- |
| 耳机开机时切换到 | 耳机开机后要切到的播放设备 |
| 耳机关机时切换到 | 耳机关机后要切回的播放设备（通常是扬声器/显示器） |
| 刷新设备列表 | 重新枚举当前的活动播放设备 |

下拉框首项是 **"(未选择)"**，不会自动预选第一个设备 —— 避免"保存配置"时把用户从未选过的设备写进配置。若已保存的设备当前不可用（被拔出/禁用），它会以 **"(已保存，当前不可用)"** 出现在列表末尾并被选中，这样保存时不会丢掉用户的选择。

> **注意**：设备列表**只包含活动的播放（Render）端点**，不包含录音设备。

### HID 检测配置

| 控件 | 默认值 | 说明 |
| --- | --- | --- |
| HID 设备匹配串 | `VID_413C&PID_A529&MI_05&COL02` | 设备实例 ID 的子串，大小写不敏感。**故意不含**结尾的 `9&2658857&0&0001` 段，因为那一段编码的是 USB 端口，换口就变 |
| 列出当前 HID 设备 | — | 打开对话框列出系统上所有 HID 集合的实例 ID，选中一行点"用作匹配串"可自动截取 `VID/PID/MI/COL` 部分填入 |
| 开机签名字节 | `08 c0 09 03 00 01 cc 0f` | 耳机开机时报告的前若干字节 |
| 关机签名字节 | `08 c0 09 03 00 01 dd 1e` | 耳机关机时报告的前若干字节 |
| 签名长度 | 8 | 参与比对的字节数（1–64） |
| 冷却 (ms) | 1500 | 命中后忽略重复报告的时长 |
| 重开延迟 (ms) | 2000 | 设备读取失败后，重新打开前的等待时长 |
| 校验末字节 XOR 校验和 | 关 | 见 [HID 签名与校验和](#hid-签名与校验和) |
| 记录未匹配的报告 | 关 | 把没匹配上任何签名的报告也写进日志，排查签名问题时打开 |

签名输入框右侧会**实时显示**解析结果与校验和状态（`8 字节，校验和 ✓`），改签名时可以立刻验证。

签名输入容错：`0x` 前缀、逗号、短横线、换行、制表符都会被忽略，所以从串口工具直接粘贴的转储也能解析。

### 操作

| 按钮 | 说明 |
| --- | --- |
| 开机自启动（写入 HKCU Run） | 勾选/取消开机自启动 |
| 保存配置 | 校验输入并写入 `config.json` |
| 开始监控 | **先保存配置**，再启动监控 |
| 停止监控 | 停止监控线程 |
| 清空日志 | 清空界面日志并删除日志文件 |
| 配置目录 | 用资源管理器打开 `%APPDATA%\USBDongle_AudioSwitch` |

> "开始监控"会连带保存配置。若已有监控在运行，会被**停止再重启** —— 因为设置是在监控线程启动时快照的，不重启不会生效。

### 界面语言

状态栏右端的语言下拉框可在**中文与英文**之间切换整个界面，立即生效、无需重启。该设置会写进 `config.json` 的 `language` 字段，重启后依然保留。默认值 **自动（跟随系统）** 会在中文 Windows 上选择中文，其余情况选择英文。

界面文字与写入日志的内容都会跟随所选语言，因此在程序运行中途切换语言，同一个日志文件里可能同时出现两种语言的行。

### 状态栏

左侧是当前状态（`就绪` / `监控中` / `已停止` / `已切换到耳机` / `已切换到扬声器` / `切换失败` / `未配置目标设备` / `已是目标设备`），中间是当前打开的 HID 设备路径（未连接时显示 `设备：未连接`），右侧是累计检测到的信号事件数，再往右是语言下拉框。

### 运行日志

界面日志与日志文件内容一致（`Logger.LineWritten` 事件驱动）。界面最多保留 **2000 行**，超出后裁剪到一半；日志文件按 **5 MB** 轮转。

日志框刻意只占一小块高度，**用鼠标滚轮翻看历史**：

- **指针悬停即可滚动**，不需要先点进去。Windows 的滚轮消息是发给*拥有焦点*的窗口而非指针下方的窗口，而日志框是只读的、很少获得焦点，所以由 `LogWheelRouter` 把消息重新投递给它。
- **向上翻看时不会被新日志拽回底部**。新日志只在视图本来就停在末尾时才自动跟随；一旦你往回翻，位置就固定住，直到你再次滚到底。

## 配置文件

位置：`%APPDATA%\USBDongle_AudioSwitch\config.json`

文件不存在时使用默认值（等同于原脚本的行为）。解析失败时会备份为 `config.json.bad` 并回退到默认值。写入采用**先写 `.tmp` 再替换**的方式，避免中途崩溃留下半个文件。

读取时容忍 UTF-8 字节序标记（BOM），因此用记事本保存过的配置文件（记事本默认会写入 BOM）能被正常读取，而不会被当成损坏文件。

```json
{
  "headsetDeviceId": "{0.0.0.00000000}.{52e88adb-13eb-485e-a66b-2642aa15eab6}",
  "speakerDeviceId": "{0.0.0.00000000}.{f80bc369-90b1-49b8-856c-cac949a23c8f}",
  "setAllRoles": true,
  "soundVolumeViewPath": "",
  "language": "auto",
  "hidMatch": "VID_413C&PID_A529&MI_05&COL02",
  "signatureOnHex": "08 c0 09 03 00 01 cc 0f",
  "signatureOffHex": "08 c0 09 03 00 01 dd 1e",
  "signatureLength": 8,
  "cooldownMs": 1500,
  "reopenDelayMs": 2000,
  "verifyChecksum": false,
  "logUnknownReports": false,
  "startMonitoringOnLaunch": true
}
```

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `headsetDeviceId` | string | `""` | 耳机开机时要切换到的端点 ID |
| `speakerDeviceId` | string | `""` | 耳机关机时要切换到的端点 ID |
| `setAllRoles` | bool | `true` | `true` 时同时设置 Console/Multimedia/Communications 三个角色（同原脚本）；`false` 时只设置 Multimedia，等同于系统的"设为默认设备" |
| `soundVolumeViewPath` | string | `""` | 可选的 `SoundVolumeView.exe` 完整路径，仅在原生 COM 调用失败时作为回退 |
| `language` | string | `"auto"` | 界面语言：`auto`、`en` 或 `zh`。无法识别的值会回退到 `auto` |
| `hidMatch` | string | `VID_413C&PID_A529&MI_05&COL02` | HID 设备实例 ID 的匹配子串 |
| `signatureOnHex` | string | `08 c0 09 03 00 01 cc 0f` | 开机签名的十六进制表示 |
| `signatureOffHex` | string | `08 c0 09 03 00 01 dd 1e` | 关机签名的十六进制表示 |
| `signatureLength` | int | `8` | 参与比对的字节数 |
| `cooldownMs` | int | `1500` | 冷却时长，`0` 视为使用默认值，上限 60000 |
| `reopenDelayMs` | int | `2000` | 重开延迟，`0` 视为使用默认值，范围 100–60000 |
| `verifyChecksum` | bool | `false` | 是否校验末字节 XOR 校验和 |
| `logUnknownReports` | bool | `false` | 是否记录未匹配的报告 |
| `startMonitoringOnLaunch` | bool | `true` | 启动时是否自动开始监控 |

`setAllRoles` 与 `startMonitoringOnLaunch` 在内部实现为可空布尔：**键缺失**（走默认值 `true`）与**显式写 `false`** 能被区分开，因此手动把任一项改成 `false` 不会被默认值覆盖。

越界或非法的值会在加载时被就近修正：匹配串为空则填默认值，签名无法解析或为空则填默认值，`signatureLength` 超出签名实际长度则被夹到合法范围。

---

## 文件位置

所有数据都在 `%APPDATA%\USBDongle_AudioSwitch\` 下，**而不是**程序目录 —— 程序会把自己写进 HKCU 的启动项，若安装在 `Program Files` 之下将没有自身目录的写权限。

| 文件 | 说明 |
| --- | --- |
| `config.json` | 配置 |
| `config.json.tmp` | 保存过程中的临时文件（正常情况下一闪而过） |
| `config.json.bad` | 配置解析失败时的备份 |
| `audio_switch.log` | 运行日志，UTF-8 |
| `audio_switch.log.old` | 轮转归档，日志达到 5 MB 时生成（会覆盖上一个归档） |

界面上的"配置目录"按钮可直接打开该目录。

---

## 开机自启动

勾选界面上的"开机自启动（写入 HKCU Run）"后，会在注册表写入：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
    USBDongle_AudioSwitch = "<程序完整路径>" --tray
```

用 **HKCU 而非 HKLM 或任务计划程序**是有意为之：不需要管理员权限，而且被切换的音频端点是**每用户**设置，做成全局项反而是错误的作用域。

若程序被移动过，启动项会指向旧路径。程序启动时检测到这种情况会在日志里给出提示，**重新勾选一次**该复选框即可修正。

---

## HID 签名与校验和

接收器在中断端点上发送约 62 字节的输入报告，**前 8 个字节**携带状态变化，其余为 0 填充。

| 事件 | 报告特征 |
| --- | --- |
| 关机（Power Off） | 单条报告 |
| 开机（Power On） | 一串共 8 条报告，开机签名是其中的第一条 |

### 匹配规则

对报告的前 `signatureLength` 个字节做**逐字节精确比对** —— 没有偏移、没有通配符。

### XOR 校验和

两条内置签名都满足同一个关系：**最后一个字节 == 前面 7 个字节的 XOR**。

```
开机：08 ^ c0 ^ 09 ^ 03 ^ 00 ^ 01 ^ cc = 0f   →  08 c0 09 03 00 01 cc 0f   ✓
关机：08 ^ c0 ^ 09 ^ 03 ^ 00 ^ 01 ^ dd = 1e   →  08 c0 09 03 00 01 dd 1e   ✓
```

注意两条签名的**第 7 个字节不同**（`cc` 对 `dd`），末字节正是随之前移而重新算出的结果 —— 两条签名各自自洽，不是笔误（界面上的实时指示器会分别显示"校验和 ✓"）。

勾选"校验末字节 XOR 校验和"后，校验的是**报告的**末字节是否等于其前面字节的 XOR。这样前 7 字节正确、末字节被读坏的报告会被拒绝，用于过滤传输中的损坏。启用该选项需要 `signatureLength >= 2`。

> **改配置后需重启监控**：`verifyChecksum` 等设置在监控线程启动时被读入局部变量，修改后要**停止再开始监控**（点"开始监控"按钮会自动完成这个循环）才会生效。

---

## 故障排查

### 找不到 HID 设备 / 日志一直显示设备不可用

1. 确认接收器已插好，设备管理器里能看到。
2. 点界面上的 **"列出当前 HID 设备"**，在列表里找到你的接收器，选中后点"用作匹配串"。程序会自动截取 `VID/PID/MI/COL` 部分 —— 这是最可靠的做法，比手抄设备管理器里的实例 ID 强得多。
3. 匹配串**不要**带结尾的 `9&2658857&0&0001` 段，那段是 USB 端口号，换口就变。

设备路径每次打开前都会重新解析，所以拔插、换 USB 口都不需要改配置。设备读取失败后会等待"重开延迟"再重试，循环往复。

### 检测不到开关机

1. 勾选 **"记录未匹配的报告"**，然后开关一次耳机，看日志里实际收到的字节。
2. 把日志里实际的报告前缀填进"开机签名字节"/"关机签名字节"。签名输入框右侧会实时显示校验和状态，方便确认。
3. 检查"签名长度"是否与实际填的字节数匹配 —— 填得比签名本身还长会直接报错。

### 检测到了但设备没切换

1. 看日志里的具体报错。常见的是 **"尚未配置目标设备"** —— 两个下拉框都要选好再点"开始监控"。
2. 若显示 **"当前默认设备已是目标设备，无需切换"**，说明程序实时读到的默认设备已经是目标了，这是正常的跳过逻辑（比如你刚手动切过）。
3. 若报 **"IPolicyConfig 切换失败"**，说明原生 COM 调用被拦截。可以下载 [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html)，在 `config.json` 的 `soundVolumeViewPath` 里填上完整路径作为回退方案。

### 只想改多媒体角色，不想动另外两个

把 `config.json` 里的 `setAllRoles` 改成 `false`。默认的 `true` 会同时设置三个角色，与原脚本一致。

### 构建报找不到目标包

正常现象，`build.ps1` 会自动回退到运行时程序集，输出里会打印 `目标包 : 未安装，改用运行时程序集：...`。不需要额外操作。若想看到真实的编译环境问题，加 `-NoFrameworkPathOverride`。

### 怎么完全退出

关闭窗口只会最小化到托盘。要真正退出必须用**托盘图标右键菜单 → 退出**。

---

## 项目结构

```
USBDongle_AudioSwitch/
├─ Program.cs                    入口：单实例、激活已有实例、全局异常兜底
├─ AppPaths.cs                   %APPDATA% 下的路径常量
├─ AppConfig.cs                  配置模型 + JSON 读写（原子替换、损坏备份）
├─ Localization.cs               所有面向用户的字符串，中英双份
├─ AppIcons.cs                   加载嵌入的多分辨率图标，按尺寸、DPI 与主题取帧
├─ SystemTheme.cs                读取外壳当前把任务栏画成深色还是浅色
├─ Logger.cs                     线程安全日志，5 MB 轮转
├─ LogWheelRouter.cs             把滚轮消息路由给指针下方的日志框
├─ AutoStartManager.cs           HKCU Run 键读写与失效检测
├─ Hex.cs                        十六进制解析/格式化、XOR 校验和
├─ MainForm.cs                   主窗体逻辑
├─ MainForm.Designer.cs          主窗体布局（纯代码，无 .resx 布局）
├─ build.ps1                     免 VS/SDK 的构建脚本
├─ audio_switch.ps1              原始 PowerShell 脚本（参考用，随构建复制到输出）
├─ Resources/
│  ├─ app.ico                    窗口/任务栏/exe 图标（9 帧，含 256）
│  ├─ tray-white.ico             深色任务栏用的托盘图标（6 帧）
│  └─ tray-black.ico             浅色任务栏用的托盘图标（6 帧）
├─ assets/
│  ├─ make_icon.py               从母版生成上面三个 .ico
│  ├─ art-black.ico              黑色线稿母版（不随程序分发）
│  └─ art-white.ico              白色线稿母版（不随程序分发）
├─ Audio/
│  ├─ AudioDeviceInfo.cs         下拉框条目的 DTO
│  ├─ AudioDeviceService.cs      Core Audio 枚举：播放设备列表、当前默认设备
│  └─ AudioSwitcher.cs           切换默认设备：IPolicyConfig → SoundVolumeView 回退
├─ Interop/
│  ├─ AudioInterop.cs            Core Audio / IPolicyConfig 的 COM 声明
│  ├─ ComApartment.cs            COM 初始化 RAII 包装（MTA）
│  ├─ SetupApi.cs                SetupAPI P/Invoke（枚举 HID 接口）
│  └─ Win32.cs                   kernel32/user32 P/Invoke、错误码描述、DPI 查询
└─ Monitoring/
   ├─ HidDeviceLocator.cs        解析匹配串 → 设备路径、枚举 HID 实例 ID
   ├─ HidMonitor.cs              HID 读取循环：打开、阻塞读、匹配、断开重连
   ├─ HidSignal.cs               PowerOn / PowerOff 枚举
   └─ MonitorController.cs       把 HID 信号接到音频切换，向 UI 暴露事件
```

### 一些实现上的取舍

- **不用 WMI**：原脚本用 `Get-PnpDevice`（WMI/CIM），启动慢且依赖 PowerShell 环境。本项目改用 SetupAPI 直接枚举，没有 `System.Management` 依赖。
- **重叠 I/O 而非同步读**：同步 `ReadFile` 在设备拔出时可能不会返回，停止监控就会卡死。`HidMonitor` 用 `FILE_FLAG_OVERLAPPED` + `CancelIoEx`，`Stop()` 能在 5 秒内收干净线程。
- **缓冲区用 `IntPtr` 固定**：`Win32.cs` 里读写缓冲区声明为 `IntPtr` 而非 `byte[]`，配合 `GCHandle.Alloc(..., Pinned)`，防止封送器在重叠操作进行中移动缓冲区。
- **切换前实时比对**：不缓存"当前默认设备"，而是每次切换前重新读取 —— 用户可能手动改过默认设备，用缓存会错误地跳过本该执行的切换。

## 致谢

本项目受 [Meladon90/Audio-Switch](https://github.com/Meladon90/Audio-Switch) 启发。
