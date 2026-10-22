# 物联网遥测流数据处理与自定义规则预警系统

一个基于 .NET 的物联网遥测数据采集、处理、持久化和报警系统。

项目面向多设备、多管理节点和高频遥测数据场景，采用网关、Backend 工作节点和设备采集端分层设计，实现了设备管理、数据上报、规则校验、异常预警、历史查询和权限控制等功能。

## 项目特点

- 基于 Redis 的 Backend 节点服务发现和心跳注册
- Frontend 网关根据设备归属进行请求路由
- 支持多台 Backend 节点共同处理设备数据
- 基于 CPU 核心数进行设备内存管理器分片
- 使用 `Channel` 构建异步数据处理流水线
- 支持批量写入遥测数据，减少数据库 I/O 压力
- 内置轻量级表达式解析器，支持自定义报警规则
- 支持 JWT 身份认证和三级角色权限控制
- 使用 Redis 实现用户、权限和设备信息缓存
- 使用 MediatR 解耦设备注册、删除、日志和报警流程
- 支持设备软删除和历史数据查询
- 提供测试用设备数据上传程序
- 提供基础 Vue API 客户端和角色权限封装

## 系统架构

```text
┌─────────────────────┐
│        GUI                                                        │
│     Vue 3 前端                                                 │
└──────────┬──────────┘
           │ HTTP + JWT
           ▼
┌─────────────────────┐
│      Frontend                                                  │
│  API 网关与权限服务                                      │
└──────────┬──────────┘
           │ Redis 服务发现
           │ 请求路由
           ▼
┌─────────────────────┐
│       Backend                                                   │
│ 设备管理与数据处理节点                                 │
└──────────┬──────────┘
           │
           ├── 设备注册与管理
           ├── 遥测数据接收
           ├── 规则解析与报警
           └── 批量数据持久化
           ▲
           │ HTTP
┌─────────────────────┐
│       Device                                                     │
│ 设备数据采集/模拟端                                      │
└─────────────────────┘
```

## 项目结构

```text
src/ASP
├── Frontend
│   ├── Program.cs
│   └── Source
│       ├── AuthController.cs
│       ├── BackendManager.cs
│       ├── Config.cs
│       ├── Controller.cs
│       ├── DataBase.cs
│       ├── DataBaseOp.cs
│       ├── Machine.cs
│       ├── MachineManager.cs
│       ├── Tool.cs
│       └── UpdateService.cs
├── Backend
│   ├── Program.cs
│   └── Source
│       ├── AllDeviceManager.cs
│       ├── Controller.cs
│       ├── Device.cs
│       ├── DeviceDBService.cs
│       ├── DeviceManager.cs
│       ├── Expression.cs
│       ├── MachineRegistry.cs
│       ├── UploadDataService.cs
│       └── ...
├── Device
│   └── Program.cs
└── GUI
    └── src
        ├── App.vue
        ├── main.js
        └── utils
            ├── api.js
            └── role.js
```

## 核心模块

### Frontend 网关

Frontend 是系统对外提供服务的统一入口，主要负责：

- 用户登录和 JWT Token 签发
- 用户注册、删除和角色修改
- 用户与 Backend 节点之间的权限映射
- Backend 节点发现和请求转发
- 设备类型管理
- 设备注册、查询和删除
- 设备历史数据查询
- 报警查询和报警确认
- 用户权限和设备归属校验

Frontend 会定期从 Redis 获取在线 Backend 节点，并根据设备所属的管理节点将请求转发到目标 Backend。

### Backend 工作节点

Backend 运行在实际的设备管理机器上，主要负责：

- 设备类型和字段定义管理
- 设备注册和软删除
- 设备数据接收
- 设备数据内存处理
- 自定义规则表达式求值
- 遥测数据批量落库
- 报警记录维护
- 向 Redis 注册节点地址并定期续期

### Device 数据采集端

Device 是用于模拟或采集设备数据的客户端程序，当前包含测试数据生成逻辑，可以：

- 从配置文件读取设备编号和 Backend 节点编号
- 从 Redis 获取目标 Backend 地址
- 定时生成测试遥测数据
- 通过 HTTP 上传设备数据

实际接入真实设备时，可以将数据生成部分替换为串口、TCP、MQTT 或其他设备协议读取逻辑。

### GUI 前端

GUI 当前包含基础 Vue 3 工程和 API 客户端封装，支持：

- 登录
- JWT Token 保存
- 用户退出
- 设备和设备类型接口调用
- 不同角色的 API 能力封装

角色客户端分为：

```text
ViewerClient
└── OperatorClient
    └── AdminClient
```

## 数据处理流程

```text
设备采集数据
    │
    ▼
Device 上传数据
    │
    ▼
Backend UploadData 接口
    │
    ▼
MediatR 事件分发
    │
    ▼
DeviceManager 分片队列
    │
    ▼
内存规则计算与报警判断
    │
    ├── 正常数据
    │
    └── 异常数据，写入报警队列
    │
    ▼
UploadDataService 批量处理
    │
    ├── 保存字段历史值
    └── 更新报警记录
```

## 自定义规则表达式

Backend 内置轻量级表达式解析器，用于设备数据的实时校验。

支持以下类型：

- 数字
- 字符串
- 布尔值
- 变量
- 括号
- 四则运算
- 比较运算
- 逻辑运算
- 一元运算

支持的运算符：

```text
+   -   *   /   %
==  !=
<   <=  >   >=
&&  ||
!
```

示例：

```text
temperature < 80 && weight > 10
```

```text
temperature >= 0 && temperature <= 100
```

```text
status == "normal"
```

规则表达式会在 Backend 内存中执行。设备数据上报后，系统会先更新设备当前状态，再根据表达式计算是否触发报警。

## 数据批处理机制

系统使用 `System.Threading.Channels.Channel` 缓冲设备数据和待持久化事件。

数据处理过程包含以下步骤：

1. Backend 接收设备数据
2. 根据设备 ID 将数据分配到对应的内存管理器
3. 内存管理器更新设备当前字段值
4. 执行设备规则表达式
5. 生成遥测数据和报警事件
6. `UploadDataService` 从 Channel 中读取事件
7. 根据批量大小和最大等待时间聚合数据
8. 使用 EF Core 批量写入 SQL Server

批处理支持两个结束条件：

- 达到最大批量数量
- 达到最大等待时间

这样可以在高吞吐和低延迟之间取得平衡。

## 设备 ID 设计

设备 ID 使用 64 位整数编码：

```text
高 32 位：Backend 节点 ID
低 32 位：节点内本地自增 ID
```

示意：

```text
DeviceId = (MachineId << 32) | LocalDeviceId
```

通过设备 ID 可以直接解析设备所属 Backend 节点：

```text
OwnerId = DeviceId >> 32
LocalId = DeviceId & 0xFFFFFFFF
```

该设计使 Frontend 能够直接完成设备请求路由，不需要额外查询设备所在节点。

## 用户角色

系统包含三级用户角色：

| 角色 | 说明 |
| --- | --- |
| `Viewer` | 只读用户，可以查看设备和历史数据 |
| `Operator` | 可以查看数据、管理设备、查询和确认报警 |
| `Admin` | 拥有用户管理、权限管理和设备类型管理权限 |

Frontend 使用 JWT Claims 保存用户 ID、用户名和角色信息，并通过 ASP.NET Core 授权策略限制接口访问。


### 数据存储

#### UserDB

Frontend 使用的数据库，包含：

- 用户名、密码哈希和角色
- 用户与 Backend 管理节点之间的权限映射

密码使用 BCrypt 哈希保存，不以明文存储。

#### DeviceDB

Backend 使用的数据库，包含：

- 设备类型和字段定义
- 设备信息
- 遥测字段历史值
- 当前报警记录

设备删除采用软删除，历史数据不会随设备删除而一并清除。

## 技术栈

### 后端

- C# / .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- Redis
- JWT Bearer Authentication
- MediatR
- MessagePack
- BCrypt.Net

### 前端

- Vue 3
- JavaScript
- Fetch API

## 运行环境

运行项目需要安装：

- .NET 10 SDK
- Node.js 和 npm
- SQL Server
- Redis

确保 SQL Server 和 Redis 已启动，并且运行项目的机器能够访问相应服务。

## 配置

Frontend 和 Backend 默认从运行目录读取 `config.json`。配置文件不存在时会生成默认配置。

### Frontend 配置

Frontend 配置包含 Redis、JWT 和用户数据库连接信息。示例：

```json
{
  "RedisConnectionString": "localhost:6379",
  "RedisInstanceName": "Cache_",
  "JwtIssuer": "FrontendGateway",
  "JwtAudience": "FrontendUsers",
  "ConnectionString": "Server=localhost;Database=UserDB;User Id=sa;Password=CHANGE_ME;TrustServerCertificate=True;",
  "JwtExpireMinutes": 10
}
```

### Backend 配置

Backend 配置包含节点 ID、监听端口、数据库和 Redis 设置。示例：

```json
{
  "MachineId": 1,
  "UploadTime": 5000,
  "ChannelMax": 5000,
  "MaxBatchSize": 1000,
  "MaxWaitTime": 1,
  "Port": 5000,
  "ConnectionString": "Server=localhost;Database=DeviceDB;User Id=sa;Password=CHANGE_ME;TrustServerCertificate=True;",
  "RedisConfiguration": "localhost:6379",
  "RedisInstanceName": "Cache_"
}
```

每台 Backend 节点应配置唯一的 `MachineId`。Frontend 与 Backend 的 Redis 地址和 `RedisInstanceName` 必须保持一致。

### Device 配置

Device 配置包含 Redis 地址、实例前缀、目标 Backend 节点和设备 ID。示例：

```json
{
  "RedisConnectionString": "localhost:6379",
  "RedisInstanceName": "Cache_",
  "MachineId": 1,
  "DeviceId": 4294967297
}
```

`DeviceId` 必须对应已注册设备的完整设备 ID。该项目中的 Device 当前用于模拟数据上传。

> 请勿将真实数据库密码、生产环境密钥或其他凭据提交到仓库。

## 启动项目

项目文件的实际路径和名称可能随仓库版本变化。可先在仓库根目录运行：

```bash
dotnet sln list
```

### 启动 Backend

```bash
dotnet run --project src/ASP/Backend
```

Backend 启动后会向 Redis 注册当前节点地址，并周期性续期。多节点部署时，为每个 Backend 配置不同的 `MachineId`。

### 启动 Frontend

```bash
dotnet run --project src/ASP/Frontend
```

Frontend 启动后会创建用户数据库。若用户表为空，会创建默认管理员，并将初始密码输出到控制台。首次启动后请及时登录并修改或妥善保管凭据。

### 启动 Device

```bash
dotnet run --project src/ASP/Device
```

Device 会从 Redis 查询配置的 Backend 节点，并持续上传模拟遥测数据。

### 启动 GUI

```bash
cd src/ASP/GUI
npm install
npm run dev
```

GUI 当前包含 API 客户端和角色客户端封装；实际页面仍需在 Vue 应用中实现。开发服务器的代理和部署路径应根据当前 Vite 配置调整。

## API 概览

API 使用 POST 请求，Frontend 的受保护接口需要在请求头中携带 JWT：

```http
Authorization: Bearer <token>
Content-Type: application/json
```

### 认证与用户管理

| 接口 | 权限 | 说明 |
| --- | --- | --- |
| `POST /api/Auth/Login` | 匿名 | 登录并获取 JWT |
| `POST /api/Auth/Register` | Admin | 创建用户 |
| `POST /api/Auth/RemoveUser` | Admin | 删除用户 |
| `POST /api/Auth/ModifyRole` | Admin | 修改用户角色 |

### 设备管理与数据查询

| 接口 | 权限 | 说明 |
| --- | --- | --- |
| `POST /api/Machine/MyMap` | 已登录 | 查询当前用户可访问的节点 |
| `POST /api/Machine/GetMachines` | 已登录 | 查询可访问的在线节点 |
| `POST /api/Machine/GetDevices` | 已登录 | 查询节点下的设备 |
| `POST /api/Machine/GetDevice` | 已登录且有节点权限 | 查询设备 |
| `POST /api/Machine/RangeValue` | 已登录且有节点权限 | 查询设备历史数据 |
| `POST /api/Machine/RegisterDevice` | Operator / Admin | 注册设备 |
| `POST /api/Machine/RemoveDevice` | Operator / Admin | 删除设备 |
| `POST /api/Machine/AllDeviceType` | Operator / Admin | 查询设备类型 |
| `POST /api/Machine/RegisterDeviceType` | Admin | 创建设备类型 |
| `POST /api/Machine/QueryAlertDevices` | Operator / Admin | 查询节点报警 |
| `POST /api/Machine/ConfirmAlert` | Operator / Admin | 确认报警 |

Backend 的 `/api/DeviceOps/UploadData` 接口供设备端或采集服务上传数据，通常不应直接暴露到公网。

## 开发说明

- Frontend 使用 Redis 中的机器注册信息维护 Backend 节点列表。
- Backend 的 `UploadTime` 控制设备数据进入持久化流程的最小间隔，单位为毫秒。
- `ChannelMax` 设置设备处理队列容量，`MaxBatchSize` 和 `MaxWaitTime` 控制批量写入行为。
- 设备类型的校验表达式引用设备字段定义中的字段名称。
- 数据查询跨度上限由 Backend 接口限制，当前最大为 3600 秒。
- 当前数据库初始化使用 EF Core `EnsureCreatedAsync()`，尚未使用迁移脚本。

## 当前状态

项目包含 Backend 数据处理和预警主流程、Frontend 网关与权限接口、Device 测试上传程序，以及 GUI 的基础 API 客户端封装。GUI 的可视化管理页面和报警查询确认页面仍待完善。
