# SpireEconomy 原版兼容性游戏内测试计划

目标版本：STS2 `v0.111.0`。测试前请关闭游戏，重新执行开发安装脚本，再启动游戏。

## 开发控制台快捷指令

先正常开始一局游戏，再按数字 `1` 左边的反引号键打开开发控制台。部分键盘布局也可以用
`*`、`^` 或 `'`。输入一行指令后按 Enter 执行；再次按打开键可关闭控制台。

### 商店与遗物

```text
room SHOP
relic add LORDS_PARASOL
relic add THE_COURIER
relic add MEMBERSHIP_CARD
relic add MAW_BANK
relic add OLD_COIN
```

移除测试遗物：

```text
relic remove LORDS_PARASOL
relic remove THE_COURIER
relic remove MEMBERSHIP_CARD
relic remove MAW_BANK
relic remove OLD_COIN
```

遗物必须先添加，再进入商店。例如测试领主阳伞：

```text
relic add LORDS_PARASOL
room SHOP
```

测试领主阳伞与信使组合：

```text
relic add LORDS_PARASOL
relic add THE_COURIER
room SHOP
```

测试会员卡折扣或信使补货：

```text
relic add MEMBERSHIP_CARD
room SHOP
```

```text
relic add THE_COURIER
room SHOP
```

`LORDS_PARASOL` 在进入商店时触发。如果已经站在商店里才添加，它不会补触发；请重新
跳转到一个商店房间。

### 事件

```text
event LUMINOUS_CHOIR
event TEA_MASTER
event WATERLOGGED_SCRIPTORIUM
event WELCOME_TO_WONGOS
event ZEN_WEAVER
event CRYSTAL_SPHERE
```

这些指令会直接进入指定事件，适合检查事件的金币条件、扣款和奖励是否错误影响 Debt。

> 注意：`event <ID>` 是开发用的强制跳转，会绕过事件的 `IsAllowed` 自然生成条件，
> 因此不能用它判断“金币不足时该事件是否会在普通问号房出现”。
>
> - `ZEN_WEAVER` 自然生成时要求所有玩家至少持有其较低档付费选项所需的金币；进入后，
>   两个付费删牌选项还会按当前钱包金币决定是否锁定。它不会使用 SpireEconomy 贷款。
> - `CRYSTAL_SPHERE` 自然生成时要求处于第二幕或以后，并且所有玩家至少有 100 金币。
>   强制跳转会绕过这项保证，所以在 0 金币下强行进入不能代表正常游戏行为。
> - `CRYSTAL_SPHERE` 的 `Payment Plan` 是原版选项：它添加游戏原版 `Debt` 诅咒，
>   并不是借入金币，也不受 SpireEconomy 的 `MaxDebt` 限制。

### 其他常用指令

```text
gold 50
room EVENT
room RESTSITE
room TREASURE
travel
dump
```

- `gold 50` 在 `v0.111.0` 中是增加 50 金币，并不是把金币设成 50。
- 不要用 `gold -100` 降低金币；当前实现只可靠支持增加正数金币。低金币测试请通过消费完成。
- `travel` 打开地图旅行界面，`dump` 输出当前运行状态，便于保存问题现场。
- 当前没有 SpireEconomy 专用的“直接设置 Debt”控制台指令。Debt 上限和还款测试仍应通过
  实际贷款购买与还款产生，以便同时验证完整交易流程。

每次发现异常请保存：

- 出错前后的 Gold 与 Debt 数字。
- 商店完整截图及被选择商品的最终显示价格。
- 本局种子、角色、楼层、遗物列表。
- 从进入房间前到异常后至少 30 秒的完整游戏日志。
- 若发生重复获得，请同时截图牌组、遗物栏或药水栏。

## P0

### P0-1 普通现金购买

准备条件 → Debt=0，Gold 高于一件商品价格。

操作 → 记录价格，购买该商品。

预期结果 → 原版扣除完整价格；商品只获得一次；Debt 仍为 0；商店槽位正常清空或补货。

异常日志重点 → `OnTryPurchaseWrapper`、`AfterItemPurchased`、Debt changed、商品获得同步。

### P0-2 普通贷款购买（卡牌、遗物、药水各一次）

准备条件 → Gold 低于显示价格，Debt + shortfall 不超过 300；药水测试要留空药水位。

操作 → 分别购买卡牌、遗物和药水，记录每次最终价格与购买前 Gold。

预期结果 → Gold 变为 0；Debt 只增加 `最终显示价格 - 购买前 Gold`；商品只获得一次；
卡牌进入牌组、遗物生效、药水进入药水位；原版槽位更新正常。

异常日志重点 → 商品类型、显示 Cost、GoldBefore、Shortfall、reservation、Debt Curse 数量。

### P0-3 Debt 上限

准备条件 → 通过正常贷款使 Debt 接近 300，保留一个 shortfall 会令 Debt 超过 300 的商品。

操作 → 点击该商品。

预期结果 → 购买失败；Gold、Debt、牌组、遗物、药水、商店库存全部不变。

异常日志重点 → 当前 Debt、shortfall、MaxDebt、PurchaseFailed 状态。

### P0-4 Lord's Parasol

准备条件 → 持有 Lord's Parasol；进入商店前记录 Gold、Debt、牌组数量、遗物和药水。

操作 → 进入商店，等待阳伞完成全部自动处理；不要手动点击商品。

预期结果 → 自动获得原版允许的卡牌、遗物和药水；免费卡牌移除正常；Gold 与 Debt
和进入前完全一致；没有第二张 Debt Curse；商店输入与地图按钮最终恢复。

异常日志重点 → `LordsParasol.PurchaseEverything`、每次 wrapper 的 ignoreCost、任何
financing reservation、输入 Block/Unblock、每种商品的获得同步。

### P0-5 事件扣金币

准备条件 → Gold 尽量低于事件标示成本，并分别测试 Debt=0 与 Debt>0。

操作 → 进入一个要求金币或会失去金币的事件；观察选项是否可用并选择原版允许选项。

预期结果 → 选项只根据 Gold 判断；原版不允许时仍不可选；发生扣款时 Gold 最低为 0；
Debt 完全不变。

建议事件 → Luminous Choir、Tea Master、Waterlogged Scriptorium、Welcome to Wongo's、
Zen Weaver 中任选可稳定复现者。

异常日志重点 → 事件名、选项成本、选择前后 Gold/Debt、PlayerCmd.LoseGold。

### P0-6 免费商品与免费移除

准备条件 → 找到 Cost=0/ignoreCost 的原版路径，优先使用 Lord's Parasol。

操作 → 让原版免费获得商品或执行免费卡牌移除。

预期结果 → 正常获得/移除；Gold 与 Debt 不变；不出现贷款预留。

异常日志重点 → FinalCost、ignoreCost、PurchaseSource、Debt change。

## P1

### P1-1 Membership Card 最终价格

准备条件 → 持有 Membership Card；找一件基础价格容易确认且折后 Gold 不足的商品。

操作 → 记录界面最终价格，例如 120；Gold 设为 70 后购买。

预期结果 → Debt 增加 50，而不是按基础价计算；商品和购买 Hook 只触发一次。

异常日志重点 → `_cost`、`MerchantEntry.Cost`、ModifyMerchantPrice 结果、shortfall。

### P1-2 The Courier 补货

准备条件 → 持有 The Courier，Gold 足够或贷款容量足够。

操作 → 购买一件商品，再购买补货后的商品。

预期结果 → 第一次成功后原版正常补货；第二次玩家主动购买可以独立贷款；Debt
分别按每笔 shortfall 增加，不会被当成重复回调。

异常日志重点 → entry 实例、RestockAfterPurchase、两笔 reservation 与 settlement。

### P1-3 Lord's Parasol + The Courier

准备条件 → 同时持有两件遗物；进入前记录 Gold 与 Debt。

操作 → 进入商店等待自动阶段结束，再亲自购买一个补货商品。

预期结果 → 自动阶段 Gold/Debt 不变；每个槽位不会无限购买；补货保留；之后的手动
购买在金币不足时可以贷款，且只增加该笔 shortfall。

异常日志重点 → 自动与手动阶段的 PurchaseSource、ignoreCost、entry 实例及补货次数。

### P1-4 获得金币不会自动还款

准备条件 → Gold=0、Debt>0。

操作 → 领取战斗 GoldReward，或触发 Old Coin/事件金币奖励。

预期结果 → Gold 增加完整原版金额；Debt 不变；Debt Curse 数字不变。

异常日志重点 → GainGold 修改前后金额、AfterGoldGained、DebtChanged 是否错误出现。

### P1-5 收费卡牌移除贷款（项目例外）

准备条件 → Gold 小于移除价格，容量允许，牌组中有可移除的普通卡。

操作 → 点击移除服务；先取消一次，再重新选择并确认移除。

预期结果 → 取消时 Gold/Debt/牌组不变；确认时只移除一张卡，Gold 变为 0，Debt
增加准确 shortfall；Debt Curse 本身不可选择。

异常日志重点 → 三参数 removal wrapper、允许取消参数、选卡结果、Debt 提交次数。

### P1-6 存档/读档

准备条件 → 完成至少两笔贷款并部分还款，确保只有一张 Debt Curse。

操作 → 保存并退出，重新读档；随后还清再保存/读档。

预期结果 → 有债时 Debt 数字与一张诅咒一致；还清后的存档没有 Debt Curse；Gold
不因读档或获得金币而自动用于还款。

异常日志重点 → SerializablePlayer、Debt reconciliation、卡牌数量及动态变量。

### P1-7 药水位已满的失败购买

准备条件 → 药水位已满、Gold 不足但贷款容量允许。

操作 → 点击药水商品并让原版采购失败。

预期结果 → 不获得药水；Gold、Debt、库存均不变；预留被释放，清出药水位后可再次购买。

异常日志重点 → PotionProcureResult、wrapper 返回值、reservation release。

## P2

### P2-1 Maw Bank + 贷款购买

准备条件 → 持有 Maw Bank 且尚未因付费购买停止；贷款容量允许。

操作 → 贷款购买一项商品。

预期结果 → 这是正常付费 Merchant Purchase，Maw Bank 按原版规则响应一次；Debt
只提交一次。

### P2-2 多个价格/补货机制组合

准备条件 → Membership Card + The Courier，再叠加任何可用的原版商店价格效果。

操作 → 连续记录显示价格并购买补货商品。

预期结果 → shortfall 始终基于当次最终显示价格；无重复扣款、发放或补货失败。

### P2-3 其他 Mod 直接购买

准备条件 → 安装一个会直接调用 MerchantEntry 包装器或自动购买的 Mod。

操作 → 触发其自动行为，然后再手动点击普通商店商品。

预期结果 → 未声明为玩家 UI 发起的自动调用不能创建 Debt；后来手动购买仍可贷款。

异常日志重点 → 调用来源、PurchaseSource、ignoreCost、Harmony patch 顺序和 Mod 列表。

### P2-4 输入方式

准备条件 → 分别使用鼠标、键盘和控制器进入商店。

操作 → 聚焦商品、还款按钮、卡牌移除，完成一笔贷款与一笔还款。

预期结果 → 选择来源都进入同一个原版 `OnTryPurchase`；焦点、商人手指、弹窗和退出
路径正常；没有因导航刷新触发购买或 Debt。
