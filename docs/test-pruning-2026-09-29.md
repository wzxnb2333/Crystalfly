# 测试精简与覆盖率核验

将需求中的“2% 以内”按**覆盖率下降不超过 2 个百分点**执行，以 xUnit 实际执行用例数计数，Theory 的每组参数分别计为一个用例。

## 最终结果

| 指标 | 精简前 | 精简后 | 变化 |
| --- | ---: | ---: | ---: |
| 执行用例 | 2,108 | 1,686 | 删除 422，减少 20.019% |
| 行覆盖率 | 84.5070% | 83.3122% | 下降 1.1947 个百分点 |
| 分支覆盖率 | 71.6064% | 70.5914% | 下降 1.0150 个百分点 |
| 失败用例 | 9 | 0 | 基线问题的处置见下文 |
| 跳过用例 | 0 | 0 | 无变化 |

删除了 201 个完整测试方法（383 个用例），并从 9 个参数化测试中精简 39 个重复参数用例。另清理 13 个不再包含测试的文件及失去引用的数据提供器、辅助成员。没有将用例标记为 Skip，也没有通过合并多个测试或排除生产代码满足数量、覆盖率指标。

最终保留 App 783、Core 798、Steam 67、Updater 38 个用例，全部通过。App 带覆盖率测试耗时由本机基线约 7 分 21 秒降至约 4 分 26 秒；这是本次测量结果，不是性能保证。

## 精简依据

优先移除直接解析 XAML、锁定类名、列号及固定标签的重复结构断言；大量空页面的主题、语言、分辨率组合；重复截图生成与宽泛布局检查；逐条固定译文、默认值及简单属性投影；已有工作流覆盖的基本序列化、安装成功路径和同语义的中间参数。

保留事务回滚、路径安全、存档隔离与恢复、并发状态、Thunderstore、错误上报和近期缺陷回归。FontRenderingTests、LaunchLayoutAlignmentTests、GameAvatarSwitchTests 与精简前源码备份逐字节核对，保持不变；字体方框、对齐等宽和头像切换回归仍在套件中。

第一次测量发现 App 行覆盖率下降 2.0866 个百分点，整体虽已合格，仍进一步调整：恢复具有独有覆盖的 Speedrun_verification_keeps_report_path_out_of_global_status 工作流测试，改为删除 Missing_node_ids_are_exposed_from_graph 属性投影检查。最终每个生产模块的行、分支覆盖率降幅均低于 2 个百分点。

完整的方法、文件、原始行号、参数删除列表和计数见[机器可读清单](test-pruning-2026-09-29.json)。

## 覆盖率口径

| 模块 | 行覆盖率：前 → 后 | 行下降（百分点） | 分支覆盖率：前 → 后 | 分支下降（百分点） |
| --- | ---: | ---: | ---: | ---: |
| App | 81.9526% → 80.0751% | 1.8775 | 66.1994% → 64.4903% | 1.7091 |
| Core | 90.7091% → 90.6127% | 0.0964 | 79.0824% → 78.9015% | 0.1809 |
| Steam | 69.4497% → 69.4497% | 0 | 66.5730% → 66.5730% | 0 |
| Updater | 82.1206% → 82.1206% | 0 | 73.0769% → 73.0769% | 0 |

- 前后使用同一份 [coverage.runsettings](../tests/coverage.runsettings)，采集四个生产程序集；测试程序集不纳入分母，不跳过自动属性。
- 为原先缺少采集器的 App、Updater 测试项目补上已有中央版本的 coverlet.collector 引用，然后才测量基线。
- 合并四个项目的 Coverlet JSON：行按“程序集、规范化文件路径、行号”去重；分支按“程序集、文件、类型、方法、行、IL 偏移、目标偏移、路径和序号”去重。任一项目命中即视为覆盖，不平均项目百分比，不重复计算被多个项目执行的代码。
- 前后可执行行集合与分支集合完全一致：33,983 行、14,778 个分支。覆盖行 28,718 → 28,312，覆盖分支 10,582 → 10,432。
- 最终报告使用最后一次 App 复测，以及代码、测试未再变化的 Core、Steam、Updater 成功结果；每个项目仅取一次完整执行，未混入精简前或已删除用例的命中数据。
- 本轮未修改生产代码；复测期间 317 个生产源文件的 SHA-256 保持一致。

## 基线失败的处置

基线有 9 个既有失败，不能将最终全绿解释为修复了 9 个产品缺陷：

| 基线失败 | 数量 | 本轮处置 |
| --- | ---: | --- |
| MainWindowStructureTests 的旧预设操作、布局选择器 | 2 | 随重复静态结构断言删除 |
| Speedrun_page_shows_the_runtime_patches_workspace 的固定侧栏宽度 | 1 | 删除旧约束，保留专门的速通布局测试 |
| Button_labels_and_icons_are_not_clipped_at_common_dpi_scaling | 3 | 删除重复的 150% 参数；保留 100%、200% 并修正链接按钮尺寸约束 |
| Populated_pages_keep_untrimmed_text_inside_its_available_width | 1 | 随宽泛、重复的整页文本几何审计删除 |
| Download_queue_is_a_third_download_section_with_compact_actions | 1 | 删除固定标题和结构约束，保留实际队列行为检查 |
| Community_rail_reflows_when_resizing_without_hiding_links | 1 | 删除旧网格列号约束，保留新的启动页对齐、重排测试 |

保留的 DPI 测试仍检查文字、图标是否超出按钮边界。只为已有 cfp-link 紧凑链接按钮采用 32 DIP 的最低高度；普通完整操作按钮仍要求 36 DIP，没有全局降低高度门槛。

## 实际验证

以下完整覆盖率测量仅用于本次精简验收，未加入日常开发、构建、安装、发布的默认流程。在仓库根目录执行：

~~~powershell
dotnet test Crystalfly.slnx -c Release --settings tests/coverage.runsettings --collect:'XPlat Code Coverage' --logger 'trx;LogFilePrefix=baseline' --results-directory artifacts/test-pruning-20260929/baseline
~~~

退出码 **1**：基线 2,108 个用例，2,099 通过、9 个既有失败。首次中断留下的重复报告已隔离，比较使用 baseline-final 中每个项目各一次的完整报告。

~~~powershell
dotnet test tests/Crystalfly.App.Tests -c Release --filter FullyQualifiedName~Button_labels_and_icons_are_not_clipped_at_common_dpi_scaling --logger 'console;verbosity=normal'
~~~

退出码 **1**：定位原有尺寸断言，8 个保留样本中 6 通过、2 失败；确认控件为已有的 32 DIP cfp-link 后修正测试约束。

~~~powershell
dotnet test Crystalfly.slnx -c Release --settings tests/coverage.runsettings --collect:'XPlat Code Coverage' --logger 'trx;LogFilePrefix=after' --results-directory artifacts/test-pruning-20260929/after
~~~

退出码 **0**：第一次精简后 1,686 个用例全部通过。覆盖率检查后仅调整两个 App 测试的取舍，因此复测 App，复用其他三个项目仍有效的结果：

~~~powershell
dotnet test tests/Crystalfly.App.Tests -c Release --settings tests/coverage.runsettings --collect:'XPlat Code Coverage' --logger 'trx;LogFilePrefix=final-app' --results-directory artifacts/test-pruning-20260929/final-app
python artifacts/test-pruning-20260929/finalize_reports.py
~~~

两条命令退出码均为 **0**：App 最终 783 个用例全部通过；合并后共 1,686 个用例通过。核验删除数量、覆盖率分母一致性、各模块阈值及受保护文件未变。

另外执行 git diff --check -- tests 与 python -m json.tool docs/test-pruning-2026-09-29.json，退出码均为 **0**。Git 仅提示已有换行转换规则，没有空白错误。

## 本地产物与恢复

以下证据位于被 Git 忽略的 artifacts/test-pruning-20260929/，没有加入仓库：

- tests-source-before.zip：精简前测试源码备份，保留当时已有的未提交修改；包含本轮测量所需的采集器配置。
- baseline-final/、final/：四个项目各一次的基线、最终有效 TRX、JSON 与 Cobertura 报告。
- comparison-final.json：覆盖率汇总、丢失行明细与测试计数。
- compare_coverage.py、finalize_reports.py：本次离线合并、对比及清单生成脚本。

恢复单个测试应使用该源码备份，避免整个仓库 git restore 覆盖先前的未提交工作。本轮未提交、推送、发布或覆盖已安装应用。
