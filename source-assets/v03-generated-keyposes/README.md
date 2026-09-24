# 0.3 特殊动作源图

这些 PNG 使用内置 `image_gen` 工具从仓库中的真人抠图姿势派生。参照图为 `PangBaoBaoPet/Assets/stretch_classic/frame_001.png`；第二张飞吻图和仰卧图以上一个已生成姿势为参照。源图保留在此目录；运行帧复制到 `PangBaoBaoPet/Assets/shy`、`kiss`、`roll` 后，使用仓库原有的 `repair_matte.py` 轻度收边。没有在运行时调用图像生成服务。

| 源图 | 运行帧 | 作用 |
| --- | --- | --- |
| `shy-cheek.png` | `shy/frame_002.png` | 摸头后手贴脸 |
| `kiss-hand-to-mouth.png` | `kiss/frame_002.png` | 手靠近嘴部 |
| `kiss-hand-out.png` | `kiss/frame_003.png` | 向外送出飞吻 |
| `roll-squat.png` | `roll/frame_002.png`、`frame_006.png` | 下蹲与起身 |
| `roll-side.png` | `roll/frame_003.png`、`frame_005.png` | 侧卧翻滚 |
| `roll-back.png` | `roll/frame_004.png` | 仰卧翻滚 |

## 最终采用的提示词

### shy-cheek.png

> Use case: identity-preserve. Asset type: transparent full-body PNG pose for a realistic Windows desktop pet animation. Edit the supplied cutout of the same real young East Asian man. Preserve his recognizable face, glasses, black hair, broad heavyset body, dark short-sleeve printed pajama shirt and black pants/shoes. Change only the pose and expression: a subtle embarrassed, bashful reaction after being patted on the head, shoulders slightly hunched, one hand near his cheek, a modest smile and slight blush. Keep ordinary natural human proportions, especially full-length arms and hands. Straight-on camera, entire person visible, feet at same bottom baseline, centered with generous transparent margins. Genuine alpha transparency, absolutely no room or background, no shadow, no text, no symbols, no heart, no comic style, no chibi stylization. Match original photo-real cutout quality and lighting.

### kiss-hand-to-mouth.png

> Use case: identity-preserve. Asset type: transparent full-body PNG key pose for a realistic Windows desktop pet animation. Edit the supplied reference cutout of this same real young East Asian man; preserve his recognizable face, glasses, black hair, broad heavyset build, dark short-sleeve printed pajama shirt, loose black pants and shoes. New pose: standing, a slightly awkward playful blowing-kiss gesture, one hand touching his lips with elbow naturally bent, other arm relaxed, lips slightly pursed; body still faces camera. This must look like the real roommate, not a cute cartoon. Full body visible and centered, feet on original bottom baseline, generous transparent margins, straight-on view, natural arm length and hand anatomy. Genuine alpha transparency. No backdrop, no floor shadow, no text, no hearts, no symbols, no chibi proportions.

### kiss-hand-out.png

> Use case: identity-preserve. Asset type: next transparent PNG key pose in the same realistic desktop pet kiss animation. Edit this exact cutout, keeping the real man's identity, glasses, black hair, broad build, dark printed pajama shirt, pants, shoes, body location, feet baseline, photo texture, and transparent canvas consistent. Change his right hand from covering his mouth to extending naturally forward from his face, palm up, releasing a playful blown kiss; slight forward lean and the same subtle expression. Whole person visible, natural adult arm length and five-finger hand. Genuine transparent alpha, no background, no text, no heart graphic, no cartoon stylization.

### roll-squat.png

> Use case: identity-preserve. Asset type: alpha-transparent photoreal desktop pet keyframe. Same recognizable real East Asian man as supplied reference, same glasses, face, hair, broad adult body, dark printed short-sleeve pajama top, black pants and shoes. He is in a compact deep squat preparing to roll: both shoes planted at the bottom, knees bent, torso leaning slightly forward, one hand near floor but not touching a visible floor. Full body, straight-on view, no cropped fingers or shoes, natural arm length. CRITICAL: cut out only the person with a hard clean alpha silhouette. Every pixel outside his body must be completely transparent (alpha zero). No background color, no gray/pink haze, no stage, no cast shadow, no floor, no glow, no border, no text. Photographic not illustrated.

### roll-side.png

> Use case: identity-preserve. Asset type: transparent key pose for realistic desktop pet rolling animation. Edit the supplied reference cutout of the same real East Asian man, preserving recognizable face and glasses, hairstyle, heavyset adult proportions, dark short-sleeve printed pajama shirt, black pants and sneakers with photographic texture. New physically plausible pose: lying sideways on the floor midway through a gentle clumsy roll, knees tucked, one shoulder touching the floor, arms bent protecting the head, face visible toward the viewer. Full entire body visible in a wide horizontal layout, floor contact at image lower edge, enough transparent space all around. Genuine transparent background with alpha. No setting, floor drawing, shadow, text, comic style, cute/chibi change, or floating limbs.

### roll-back.png

> Use case: identity-preserve. Asset type: second transparent key pose for the same realistic desktop pet rolling animation. Preserve this exact real man's recognizable face, glasses, hair, broad adult build, dark printed pajama shirt, black pants and sneakers. Change only the body pose to the next moment of a gentle roll on the floor: from lying on his side to lying on his back diagonally with knees curled and shoulders touching the ground, arms loosely raised for balance, still physically plausible and complete body visible. Keep the same photographic texture and wide horizontal framing with floor contact near the bottom. True fully transparent background, no backdrop, shadow, glow, text, props or cartoon style.

首个蹲姿候选有明显背景光斑，未用于运行帧。最终蹲姿来自重新生成的透明版本。源图与照片风格动作仍存在细小衣纹变化，见 [0.3 实施记录](../../docs/0.3-实施记录.md)。
