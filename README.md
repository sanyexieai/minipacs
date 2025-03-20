# MiniPACS

MiniPACS 是一个基于 C# 和 fo-dicom 开发的简单 DICOM 服务器和查看器应用程序。

## 功能特性

- DICOM 服务器功能
  - 支持 DICOM 文件的接收和存储
  - 可配置服务器端口
  - 支持启动/停止服务器
- DICOM 文件管理
  - 本地 DICOM 文件导入
  - 文件列表查看
  - 显示患者基本信息
- 文件信息显示
  - 患者姓名
  - 患者 ID
  - 检查日期
  - 检查模态

## 系统要求

- .NET 9.0 或更高版本
- Windows 操作系统

## 快速开始

1. 启动应用程序
2. 设置 DICOM 服务器端口（默认为 11112）
3. 点击"启动服务器"按钮启动 DICOM 服务器
4. 使用"导入"按钮导入本地 DICOM 文件
5. 文件列表会显示所有已导入的 DICOM 文件信息

## 开发环境

- Visual Studio 2022
- .NET 9.0
- fo-dicom 5.0.0 或更高版本

## 依赖项

- FellowOakDicom
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging

## 许可证

MIT License

## 注意事项

- 所有 DICOM 文件都存储在应用程序目录下的 DicomStorage 文件夹中
- 请确保有足够的磁盘空间用于存储 DICOM 文件
- 建议定期备份重要的 DICOM 文件 