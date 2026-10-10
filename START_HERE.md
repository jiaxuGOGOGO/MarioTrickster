# MarioTrickster：只记住这三件事

## 平时开发

双击：`OPEN_MARIOTRICKSTER_UNITY.cmd`

它会先检查当前电脑是否装对 Git、Unity 和项目分支；通过后才打开 Unity。

## 想看现在能不能安全迁移

双击：`CHECK_MARIOTRICKSTER.cmd`

它只会显示：当前分支、未保存内容、Unity 自动生成的 `.meta` 数量，以及最近一次测试结果。**不会修改、提交或推送任何文件。**

## 想“存档”到 GitHub

双击：`SAVE_MARIOTRICKSTER.cmd`

它会把已经确认的代码、接续文档和测试摘要保存成一个 Git checkpoint 并推送 GitHub。成功后看到：

```text
SUCCESS: Project checkpoint is saved to GitHub.
```

看到 `STOPPED` 时不要慌：工具没有推送任何内容，只需按窗口中列出的单一问题处理后再双击一次。

## 换电脑 / 换 Manus 账号

1. 从 GitHub 克隆 `genspark_ai_developer`；
2. 双击 `OPEN_MARIOTRICKSTER_UNITY.cmd`，按提示安装缺失工具并在 Unity 中运行 EditMode；
3. 新 Manus 对话中附上或复制 `CONTINUE_WITH_MANUS.txt` 的内容。

完整背景和紧急离线补丁方式见 `docs/AI_CONTINUE_PACK/NEW_DEVICE_NEW_ACCOUNT.md`。
