# 0.5 对话框与人像边缘小样核查

日期：2026-09-25。范围为本地开发预览，不替代正式版双服装逐帧验收。

## 底部对话框

从 `dist/PangBaoBaoPet-0.5.0-preview.1-win-x64/PangBaoBaoPet.exe` 启动，配置重定向到 Git 忽略的 `work/qa-chat-dock-20260925/`。使用 Windows UI Automation 读取窗口与控件，并调用按钮：

- 收起时，对话入口为 `ChatIcon` 按钮，人物窗口 538×613 物理像素；展开后出现聊天、对话 API 设置、人设三个标签及输入、发送、取消控件。位于屏幕底部时，人物窗口从 y=699 移到 y=359，留出下方聊天框空间。
- 在未启用外部 API 的隔离配置中输入“今天还做操吗？”并点击发送，记录到 `[本地台词·未启用外部 API] 胖宝宝：做操呢，别盯着我看。`，没有向真实服务发请求。
- 对话框收起后保留同一窗口实例；旧进程的 UI Automation 检查过闲置自动收起与 API 设置页高度变化。本次新包检查了收起、展开和本地回复；尚未在真实外部 API、不同 DPI 与多显示器上复验。

透明 WPF 分层窗口的 `PrintWindow` 截图没有完整像素，故本次只将 UI Automation 作为交互与位置证据，不声称完成实际桌面像素级视觉验收。点击头部播放新娇羞动作的自动化鼠标注入没有得到可核实的反馈；该交互仍需实机目视确认。

## 人像边缘

ImageGen 两次直接清理透明边缘都保留明显背景光晕。随后使用成熟的 [rembg](https://github.com/danielgatis/rembg/blob/main/README.md?plain=1) 2.0.85 及 `u2net_human_seg` 人像模型离线重新分割。原始批准母版保存在 `art/masters/`，哈希不变；运行资产是其派生图。模型文件与 Python 环境仅在 Git 忽略的 `work/`，没有进入仓库或 ZIP。复现入口为 `tools/assets/matte_with_rembg.py`，安装 `rembg` 和 `onnxruntime` 后以源图、新候选路径作为两个参数运行，人工核对后再复制进运行资产。

本次更新了两套站立图、黑 T 恤跳高／跳远的 10 帧关键姿势，新增黑 T 恤娇羞和单节伸展的各五帧预览。娇羞顺序是站立、抬手、挠后颈、放下、站立；抬手和放下复用同一反向过渡姿势，非补帧产物。对 `shy-neck-candidate.png` 的原图与重新分割图，按白／50% 灰／黑三种背景做了 [并列接触表](shy-matte-before-after.png)，左列是原图，右列是重新分割图。白底仍有局部浅边、发丝和衣袖附近仍需原尺寸检查，因此只按预览小样接入。

| 本地候选（Git 忽略） | SHA-256 |
| --- | --- |
| `shy-neck-candidate.png` | `F9A58A09DEE1238AE0AE0E924D4948B251C2D209FB62A904D63EEB5CE943109B` |
| `shy-neck-transition-candidate.png` | `09B067143B0D2BE0E754CD9D4AF3C170E0FCA982F91CF8BE3784381FF1E5C90D` |
| `shy-neck-rembg.png` | `E0453FCB4F0BC6C69BA1D42DE8420F211D35272450388370B7A5F3023336333B` |
| `shy-neck-transition-rembg.png` | `4DA67FB5ADEC52062FDB08953A39E9735BAC5F75B48CB16880BE3080510ECD4F` |

广播操伸展的双臂横举候选为了在 1024×1536 竖画布容纳双手，源图人物有效身高变小。制作时按脚底与头部位置把该帧设置为 470 DIP 高、下移 60 DIP；站立和举臂终点为 361 DIP 高、零偏移。[三姿势同舞台对照](stretch-scale-review.png) 显示头部与脚底大体对齐，因此这张横举帧进入单节伸展预览。另一个候选几乎重复举臂终点，未接入。当前仍只有三种独立姿势，离规划要求的 12 个有效帧和固定舞台／裁切偏移规范有距离。

## 本地验证

- `tools/build.ps1`：0 警告、0 错误。
- `tools/test.ps1`：规则、持久化恢复与模拟 API 契约通过。
- `python tools/assets/validate_assets.py`：18 个动作条目、0 个结构错误、90 帧低于 2 源像素／显示 DIP。
- `python tools/assets/validate_assets.py --release`：25 个覆盖／帧数／预览缺口，另有上述 90 帧密度问题，预期失败。
- 本地 ZIP：31,743,912 字节、213 个条目、SHA-256 `BE828AD66D0A8979C7C6FD8EF262A6086650E92C87A93896B452D63106E95EE8`；未发现私有照片、候选、工作缓存或密钥路径。

测试结束后关闭隔离配置的预览进程；未修改日常用户配置。
