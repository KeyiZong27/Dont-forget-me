# 暂存：按灰盒接口编写的 Sandbox 与续章代码

这个文件夹名以 `~` 结尾，Unity 不会导入或编译其中的任何内容。

这里的脚本来自灰盒版本那条开发线：

- `SandboxGame.cs`、`SandboxWorld.cs`、`SandboxHud.cs`、`SandboxObserver.cs`、`SandboxTarget.cs`、`SandboxPickup.cs`：300 米测试场（Sandbox 模式）。
- `RoadWorld.cs`：开场完成后扩建的第二章灰盒场景（院外空地、窄路、服务区附属房、清晨的岔路口）。
- `PlayerWorld.cs`：让主角同时服务开场与 Sandbox 的接口。
- `SandboxValidation.cs`：Sandbox 的编辑器内自动验证（原在 `Assets/Editor`）。

它们调用的是灰盒版 `OpeningWorld` / `DemoHud` 的接口（`OpeningWorld.Box`、`Shape`、`PersonModel`、`PersonLook`、`Concrete`、`ReleaseMaterials`、`DemoHud.CreateFont` 等）。画面升级之后这些接口已经不存在，主角、角色移动和后巷也已重写，所以这些脚本放回 `Assets/Scripts` 会编译失败。文件内容没有改动，`.meta` 一并保留，移植完成后可以原样搬回原位置。

没有受影响、仍在正常编译和测试的部分：

- `Assets/Scripts/Core` 下的 `JourneyState.cs`、`SandboxEquipment.cs`、`SandboxForgettingState.cs`（纯 C# 规则）。
- `Validation` 下对应的测试。
- `Assets/Scenes/Sandbox.unity`（空的入口场景；没有上面的脚本时进入它不会生成任何内容）。

续章在 `DemoGame` 里的流程逻辑（打电话、处理狗的爪伤、过夜、道别）不在这个文件夹里：它只存在于灰盒版的 `DemoGame.cs` 中，可以从提交 `17c0e17` 取回。

移植时需要做的事：

1. 在新的美术接口（`Kit` / `Art`）上提供这些脚本用到的搭建函数，人物改用 `Art` 目录里的角色模型。
2. 让 `ThirdPersonPlayer` 和 `ActorMotor` 通过 `PlayerWorld` 接口取得暂停、失败等状态，而不是直接读 `DemoGame.Instance`。
3. 把 Sandbox 场景重新加入构建列表，恢复 `DemoBuild` 里的 Sandbox 菜单。
