# 双服装飞吻重制检查（2026-09-26）

黑 T 恤与睡衣各接入“站立→手触唇→伸手飞吻→站立”四帧写真预览。第 3 帧保持原播放逻辑的爱心触发索引；爱心由桌宠界面绘制，没有烘焙进人物 PNG。黑 T 恤版此前缺失，睡衣版此前混用 330×362 旧图。两套现均为 1024×1536 RGBA、361 DIP 显示高度，脚底 alpha 位置误差约 1～2 源像素，不额外加位移。

姿势使用批准的黑 T 恤人物母版和 R02 脸部参考生成，随后以睡衣母版对同一姿势换衣；用本地 `u2net_human_seg` 清理生成图的深色光晕。准入抠图 SHA-256：黑 T 恤触唇 `B8837D62E4C3EB13743EF3F545AF2FE4F6D9D141A54D650C0B59E72B1E5512E0`、伸手 `D7A58D19FCEAB5FA790DF289E86F864862D1AB95E0B303FC119854C1724A08E1`；睡衣触唇 `FEEED5FEC8E18425BEFA7FF8A4E05B63157202491A66DFFA7ADF5B9F77D3DAEA`、伸手 `5453DE369047E6E056AFED507F2F5D2E31A75A7BBC9A7669F0342A40A5110E24`。

逐帧对照：[黑 T 灰底](black-tee-kiss-gray.png)／[白底](black-tee-kiss-white.png)／[黑底](black-tee-kiss-black.png)；[睡衣灰底](pajamas-kiss-gray.png)／[白底](pajamas-kiss-white.png)／[黑底](pajamas-kiss-black.png)。三色接触表未见大块背景残留，衣服、脸形与人物尺度大体一致。触唇→伸手之间仍缺连续中间姿势，保持“关键姿势预览”标记，不能因为低密度旧帧被替换就称动作已经流畅。
