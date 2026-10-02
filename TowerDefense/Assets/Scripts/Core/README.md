# TowerDefense.Core

Engine-free simulation logic (namespace `TowerDefense.Core`). The asmdef sets
`noEngineReferences: true`: nothing here may use `UnityEngine`. That keeps the
rules (damage, waves, economy, upgrades, ratings) unit-testable on plain .NET
via `Tools/check.sh`, and in Unity's Test Runner via `Assets/Tests/EditMode`.
MonoBehaviours in `Assets/Scripts` call into this layer; it never calls back.
