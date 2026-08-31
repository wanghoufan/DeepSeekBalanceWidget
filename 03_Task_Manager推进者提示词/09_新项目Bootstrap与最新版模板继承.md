# 新项目 Bootstrap 与最新版模板继承｜V1.9

```text
【新项目治理 Bootstrap】

不要手工复制旧项目治理文件。

必须走 bootstrap-project：

1. 验证最新 TEMPLATE_VERSION / TEMPLATE_MANIFEST。
2. 安装 Governance Assets。
3. 安装 Production Skill Files + SKILL_REGISTRY。
4. 生成 PROJECT_GOVERNANCE_ORIGIN.yaml：
   - origin_template_version
   - origin_manifest_hash
   - origin_fingerprint
   - created_at
   后续保持 immutable。
5. 生成 PROJECT_GOVERNANCE_APPLIED.yaml：
   - current_applied_template_version
   - current_applied_manifest_hash
   - current_applied_fingerprint
   - last_promotion_id
   - updated_at
6. Stale Guard 比较 Applied，不比较 Origin。
7. PASS 后才允许 ACTIVE。

如果想 Pin 旧模板：

→ 必须提供 Trusted User Approval Receipt
→ scope=TEMPLATE_PIN
→ project/version/manifest/trusted origin/expiry/one-shot 全部校验

Task Manager 不得自己批准 Pin。

项目运行中自己的 Promotion 成功后：
→ 更新 Applied
→ Origin 不变

Worker Dispatch：
→ resolve-skills
→ selected_skill_ids
→ 加载 Skill Content
→ trusted metadata.loaded_skill_ids
```
