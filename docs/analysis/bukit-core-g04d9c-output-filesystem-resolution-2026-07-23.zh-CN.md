# Bukit Core G-04D9C Filesystem / Output Graph 受控收窄台账

> 日期：2026-07-23
>
> 任务：G-04 Group 4 / Task 35
>
> G4 基线：`2.0@729088dbc2faf1bf7a20fe670e96a09b7568e7ba`
>
> 前置提交：D9A `df9edfc6`；D9B `6a281241`
>
> 状态：implementation-complete / g4-verification-pending

## 1. 原子终态

以下九项全部由 public 变为 internal，类型和成员均未删除或改名：

1. `DirectoryCopy`；
2. `DirectoryCopyOptions`；
3. `FileWriter`；
4. `Incremental.HashUtil`；
5. `IOutputFileSystem`；
6. `IOutputPathPolicy`；
7. `OutputPathSecurityException`；
8. `SafeOutputFileSystem`；
9. `SafePathResolver`。

options/interfaces/implementations 被彼此 public signature 传播，必须同一原子提交；
成员 modifier、参数、默认值、异常基类和实现关系保持。

## 2. 治理证据

current baseline 从 D9B 的 `14/478/40` 变为：

```text
14 assemblies / 469 public types / 31 candidates
```

historical manifest 保持 `closed / 136 / 136`，九项历史记录继续为
`consumer-declaration-pending / no-public-match-found /
unknown-until-voluntary-declaration`，Git blob 仍为
`7b07d6890562387010b52301e9f8716e9bf10ed1`。

新增 `G04D9COutputFilesystemGraphTests` 锁定九项 internal/exported 终态、
type kind/interface graph、核心 member names、`OutputDestinationIdentityComparer`
存在性、baseline、historical manifest 和活动治理文档。

## 3. 安全与行为下界

Task 42 必须保留并验证：

- F-01 destructive clean 对 root/home/`.git`/escape symlink/output marker 的拒绝；
- F-03 `BuildAssetOutputCollision` 在写入前报告，结构冲突和真实 filesystem
  case semantics 不变；
- `OutputDestinationIdentityComparer` 继续同时供 `AssetOutputPlan` 与
  `BuildManifestTracker` 使用；
- F-04 默认不跟随 symlink/reparse，显式 follow 仍受 source root、chain、cycle 和
  retarget 约束；
- dotfile、prune、`size-time`/`sha256`、lowercase hex 和 directory limits；
- stale owner、manifest、重复 build 和取消行为。

owner tests 包括 `DirectoryCopyTests`、`DirectoryCopyFollowSymlinksTests`、
`FileWriterTests`、`HashUtilTests`、`SafeOutputFileSystemTests`、
`AssetPipelineTests`、`BuildManifestTests` 和 `IncrementalBuildEngineTests`。

## 4. 明确未做

本任务不修改 path comparison、destination identity、collision、symlink、hash、
copy/prune、diagnostic code、exception message、manifest shape、output ownership 或
任何 writer 行为。`FileWriter`/`SafeOutputFileSystem` 仍是既有直接写入，不宣称
temp+rename filesystem atomic replacement。

没有新增 friend、reflection fallback、dynamic loader、第二套 comparer、global path
tool，也没有修改 config/schema、plugin protocol、asset URL、Labs 或外部插件。

## 5. 验证边界

按 G4 计划，Task 35 不单独运行 tests、focused gate、aggregate、AOT 或复审。
Task 42 将统一执行 Engine/Architecture、安全 owner tests、public API drift、G4
唯一 aggregate、Native AOT/package smoke 和两级只读复审。在此之前状态保持
`g4-verification-pending`。
