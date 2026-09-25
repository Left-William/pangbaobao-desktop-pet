# 0.5 动作补帧与尺度检查（2026-09-25）

状态：**可运行的关键姿势预览，未通过正式动作验收**。本记录对应工作区素材；公开 GitHub Release 仍为 0.3。批准的人物母版 `pbb-photo-05-approved` 没有改动，用户之前确认的外貌方向为“基本像，可继续”。原始参考照片、未采用的生成候选与制作缓存留在 Git 忽略目录。

## 本次接入

| 动作与服装 | 当前运行帧 | 不同 PNG | 新增的真实姿势 | 逐帧检查 |
| --- | ---: | ---: | --- | --- |
| 睡衣伸展 | 9 | 5 | 低位斜伸、水平平举、浅上举、充分上举 | [灰底逐帧排版](stretch-pajamas-9frames-gray.png) |
| 黑 T 伸展 | 9 | 5 | 同上 | [灰底逐帧排版](stretch-black-9frames-gray.png) |
| 黑 T 原地跳高 | 6 | 5 | 加入踮起蹬地、屈膝落地，原有预蹲与腾空保留 | [灰底逐帧排版](high-jump-black-6frames-gray.png) |
| 黑 T 立定跳远 | 6 | 5 | 向右蹬地、收腿腾空、屈膝落地分别制作；落点保留 85 DIP 位移 | [灰底逐帧排版](long-jump-black-6frames-gray.png) |

伸展顺序是站立→低位斜伸→平举→浅上举→充分上举→原路收势→站立，回程复用同一姿势；这不是九张不同的画。跳高与跳远从原先共用正面腾空姿势，改为不同的运动姿态。跳远最后一帧有 85 DIP 水平位移，动作结束后窗口移至落点再恢复日常动作，避免人物弹回起点。

上述排版按当前 WPF 的 430×490 DIP 窗口、底边 18 DIP、各帧 `frameDisplayHeights` 与 `frameOffsets` 渲染，供比较头、脚和边界。它不能代替真实显示器 DPI、桌面透明窗口和连续播放验收。九帧伸展的人物尺度与脚底在灰底检查图中基本一致；手势转场仍有跳切，衣纹和表情也会随生成姿势变化。跳高和跳远只有五种不同画面，不能达到规划中的 40／50 张有效运动帧。

另存白底与黑底逐帧检查：[黑 T 伸展白底](black-tee-stretch-white.png)／[黑底](black-tee-stretch-black.png)、[睡衣伸展白底](pajamas-stretch-white.png)／[黑底](pajamas-stretch-black.png)、[跳高白底](black-tee-high_jump-white.png)／[黑底](black-tee-high_jump-black.png)、[跳远白底](black-tee-long_jump-white.png)／[黑底](black-tee-long_jump-black.png)。本轮人工检查未见先前 RIFE 试验中的大块白色残留；发丝和手边仍需实际桌面分辨率复核。

## 来源、抠边与取舍

新姿势由内置 ImageGen 参考已批准母版和相邻姿势生成，1024×1536；制作中明确锁定发型、眼镜、脸形、体型、正常手臂长度和两套服装。成熟的 `rembg 2.0.85` 人像模型 `u2net_human_seg` 在隔离环境中输出透明候选，原始母版未覆盖。用于运行资产的关键新增透明候选哈希如下：

| 候选（Git 忽略目录 `art/candidates/`） | SHA-256 |
| --- | --- |
| `black-tee/stretch-mid-v-rembg.png` | `86B924FC9F66D9233B1967DEB3EAFCFA93BD4DC11BF069EFD9141BEB1E2394CF` |
| `pajamas/stretch-mid-v-rembg.png` | `494171E1750214DF5BBEAAD27BB975BBE8B8F6F225DEDE2639A9F28707115B3C` |
| `black-tee/high-jump-takeoff-rembg.png` | `218A407C24A967C8D4AFF6FAD82894AEFC51F82B5A0EBCEE84B1A393D1189D5A` |
| `black-tee/high-jump-landing-rembg.png` | `0E94C8CDF9E22EA6A88DC232DB73A2A8A9FC964236373EAB8E61F5B1B1D1D25B` |
| `black-tee/long-jump-takeoff-rembg.png` | `063F0E880FEE8E02F25CC8FB9FBEEC95AD0F2EE0A5E187B907CC6CE14ED1E871` |
| `black-tee/long-jump-air-rembg.png` | `67282EB54FDDB45EBDE0AF0C140A6B7F4DEF36742F7DE967435786E3A299867C` |
| `black-tee/long-jump-landing-rembg.png` | `5587663606AF0A9C6532BC0159D42100AF6CA855C4DE296F22B5BB953218CAD0` |

前一轮接受的低位、平举和充分上举候选也进入伸展；其文件保存在相应的 `art/candidates/`。黑 T 深色上举再次生成的一张候选与最终姿势几乎相同，未使用。现有 RIFE v4.6 对标准化的黑 T 伸展五帧试作了 20 帧 RGB 与 alpha 补帧；检查图的第 3、13 帧出现手臂消失和白色残留，整组试验留在忽略的 `work/stretch-rife-trial/`，**没有进入运行资产**。像素插值不能充当人物姿势补画。

## 校验结果与下一步门槛

`python tools/assets/validate_assets.py`：18 个动作条目，结构错误 0，72 帧仍低于 2 源像素／显示 DIP；这些低密度帧主要属于睡衣旧广播操及互动动作。`--release`：31 项失败，包含缺失的双服装动作、帧数不足、不同画面不足和“预览”动作。校验器现在按 PNG 哈希计算最低不同文件数，防止简单复制帧通过发布门槛；不同哈希也不能代替人工的动作质量判断。`tools/test.ps1` 中的规则和模拟 API 测试通过。

后续仍需给两套服装重制扩胸、体前屈、转体、踮脚、侧弯与互动动作，给跳跃补运动中间姿势，并完成引体向上道具分层、俯卧撑接地往复、街舞 Windmill 连续动作。大幅水平／倒地动作还需按规划完成固定宽舞台和空间预检。正式发布前应在桌面透明窗口、白／灰／黑背景和不同 DPI 下逐帧复核。
