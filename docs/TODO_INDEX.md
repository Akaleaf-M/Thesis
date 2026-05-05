# TODO Index

本文档汇总 `docs/ARCHITECTURE.md` 和 `docs/RUNBOOK.md` 中的 TODO，并保留作者已确认事项的 ID 追踪。分类只基于现有文档、代码可见信息和作者确认；未能确认的内容继续保留为 TODO。

| ID | 所在文档 | TODO 内容 | 类型 | 需要谁确认 | 建议下一步 | 是否阻碍当前开发 |
| --- | --- | --- | --- | --- | --- | --- |
| TODO-001 | `docs/ARCHITECTURE.md`, `docs/RUNBOOK.md` | 已确认：final projection mapping workflow 尚未最终决定。开发阶段先假设 Unity 输出为 single window，之后用 MadMapper 或 Resolume 做 mapping。 | later | hardware test / artist decision | 开发阶段按 Unity single-window output 继续；展览部署前再用实际投影设备确认 MadMapper / Resolume workflow、分辨率和 routing。 | no |
| TODO-002 | `docs/ARCHITECTURE.md` | 已确认：VCV project 将与 Unity project 同时运行，后续可能通过 UDP 向 Unity 传输数据。TD 目前可视为 leftover。 | later | artist decision / code | 暂时不把 TD 纳入 runtime；后续如果 VCV 需要控制 Unity，再定义 VCV -> UDP -> Unity 的 protocol、ports 和 receiver。 | no |
| TODO-003 | `docs/ARCHITECTURE.md` | 记录 `Unity/MotionCapture/upose/upose/upose.py` 内部 rotation math 的具体算法。 | optional | code | 阅读 Python UPose implementation，整理 pelvis、torso、shoulder、elbow、hip、knee rotation 的计算逻辑。 | no |
| TODO-004 | `docs/ARCHITECTURE.md` | 已确认：当前 collective body 继续使用 `aggregator.py` 的 quaternion average。未来可能探索 weighted / fragmented assignment，但现在不作为开发目标。 | later | artist decision / code | 保持现有 aggregator 策略；如果之后进入新融合实验，再新建设计任务，不影响当前 pipeline。 | no |
| TODO-005 | `docs/ARCHITECTURE.md` | 确认当前是否有 scene 或 workflow 使用 `UPose.cs` 的 CSV playback。 | optional | Unity Inspector / artist decision | 检查 active scene 中 `UPose.useCSV`、`csvFilePath` 设置，并询问是否需要离线 playback workflow。 | no |
| TODO-006 | `docs/ARCHITECTURE.md` | 已确认：当前主要使用 single collective avatar。Multiple delayed avatars / `PoseMemory` 可视为实验或备用机制，除非 active scene 中确认正在使用。 | later | Unity Inspector | 保持 single collective avatar 为主线；检查 `DanceScene.unity` 时只需记录 `PoseMemory` 是否实际参与 scene。 | no |
| TODO-007 | `docs/ARCHITECTURE.md` | CLOSED: active scene 中存在名为 `Avatar_Collective` 的 object。 | optional | Unity Inspector | 已记录在 architecture；无需继续跟踪。 | no |
| TODO-008 | `docs/ARCHITECTURE.md` | 记录 actual projection output path：final camera name、output resolution、display index、Unity direct output / Spout / NDI / Syphon / window capture、MadMapper / Resolume 使用情况。 | later | Unity Inspector / hardware test | 在 Unity scene 和投影机环境中确认最终输出 camera、Display、分辨率和外部 mapping 工具。 | no |
| TODO-009 | `docs/ARCHITECTURE.md`, `docs/RUNBOOK.md` | CLOSED: active Unity scene 为 `DanceScene.unity`。 | optional | Unity Inspector | 已记录在 architecture 和 runbook；无需继续跟踪。 | no |
| TODO-010 | `docs/ARCHITECTURE.md`, `docs/RUNBOOK.md` | CLOSED: active scene hierarchy, Inspector assignments, `FragmentSlot` prefab internals, UDP listeners, GLTF loading, fragment visual behavior, and Console status have been verified. No blocking Console errors observed. Known non-blocking warning: `SceneB.mp4` may show a WindowsMediaFoundation color primaries warning unless visible color shift becomes an issue. | optional | Unity Inspector / hardware test | 已记录在 architecture 和 runbook；无需继续跟踪。 | no |
| TODO-011 | `docs/ARCHITECTURE.md`, `docs/RUNBOOK.md` | 确认 exact Python environment setup：Python version、conda environment name、package versions、dependency setup。 | critical | hardware test / code | 在实际运行机器上执行 `python --version`、`pip freeze` 或 `conda env export`，并验证 `cv2`、`mediapipe`、`upose` import。 | yes |
| TODO-012 | `docs/ARCHITECTURE.md`, `docs/RUNBOOK.md` | CLOSED: accurate port design confirmed in Unity Inspector. Python camera streams send to aggregator input ports `52833-52836`; `aggregator.py` outputs collective body to Unity port `53000`; Unity direct solo streams use `52733-52736` for P1-P4. | optional | Unity Inspector / code | 已记录在 architecture 和 runbook；无需继续跟踪。 | no |
| TODO-013 | `docs/RUNBOOK.md` | 记录 current active Unity scene 的 exact expected visual composition。 | later | artist decision / Unity Inspector | 运行 active scene，截图或文字描述正常画面：avatar、solo fragments、collective fragments、background elements。 | no |
| TODO-014 | `docs/RUNBOOK.md` | 确认 current machine 的 actual camera index mapping。 | critical | hardware test | 在安装机器上运行 `list_cameras_windows.py` 或 `list_cameras_mac.py`，记录每个 camera index 对应设备。 | yes |
| TODO-015 | `docs/RUNBOOK.md` | 添加 Unity Console screenshots 或 successful run 的 exact log examples。 | optional | hardware test | 成功跑通后截取 Unity Console 和 Python terminal 输出，补入 runbook 或附图目录。 | no |
| TODO-016 | `docs/RUNBOOK.md` | 决定是否单独 normalize documentation encoding；不要仅为 comment cleanup 重写 source code。 | optional | artist decision | 先确认乱码是否影响阅读或展示；如需要，只处理 docs 文件并保留 source code 不变。 | no |

## Notes

- `docs/ARCHITECTURE.md` 和 `docs/RUNBOOK.md` 的开头说明句中也出现了 `TODO` 字样，但它们是在解释“无法确认的信息会标记为 TODO”，不是独立待办项，因此没有单独列为任务。
- 重复主题已合并到同一 ID，并在“所在文档”中列出多个来源。
- 标记为“已确认”的行保留原 TODO ID，方便追踪历史决策；这些行不再表示当前阻碍项，除非“建议下一步”中另有说明。
