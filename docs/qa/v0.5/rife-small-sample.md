# RIFE 跳跃补帧小样

日期：2026-09-25。结论：**生成了中间帧，但不能直接用于桌宠**。

- 工具：本机已有的 `rife-ncnn-vulkan.exe`，模型目录 `rife-v4.6`，可执行文件 SHA-256 `4B970319DB2814C82B15FCEED8193151560A676A9EB63F20D4877BE77B98F44F`；随附许可文件为 MIT。运行时检测到 NVIDIA GeForce RTX 4060 Laptop GPU（8 GiB）。
- 输入：黑 T 恤跳跃站立 `frame_001.png`（SHA-256 `2D54EFE35FDA3850BA1182C606F998544382E1517FA5468BBE55CCA20F6D1AA4`）和屈膝 `frame_002.png`（SHA-256 `EF8CFEFD4128F1DEB7372694CE2FB5D7D188DC4E5939F4CD91A8A658EE17C1F4`），均为 1024×1536 RGBA。
- 命令：`rife-ncnn-vulkan.exe -m rife-v4.6 -0 frame_001.png -1 frame_002.png -o mid.png`，实际用绝对／相对路径定位到上述文件。
- 输出：本地忽略目录 `art/candidates/rife-test/mid.png`，1024×1536 **RGB**，没有 alpha 通道，SHA-256 `578FE5AA256AAA315FA5EC14F1C0B40F0777957405B84899A04A9DD4410E258E`。Pillow `verify()` 能解码，但视觉查看工具未能打开该文件，因此没有完成动态或边缘视觉验收。

这次结果只证明当前 RIFE 命令不能直接保留桌宠透明层。若继续使用它，须先有独立、可靠的透明遮罩处理与重组流程，并用白／灰／黑底及原尺寸动态预览验收；不能把 RGB 输出直接塞进运行包，也不能把一帧补出多帧当作动作内容已完成。
