using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using FellowOakDicom;
using FellowOakDicom.Network;
using System.Text;
using Microsoft.Extensions.Logging;
using FellowOakDicom.Network.Client;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO;
using System.Buffers;
using FellowOakDicom.Imaging;
using FellowOakDicom.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace minipacs;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private IDicomServer _server;
    private string _storageFolder = Path.Combine(Environment.CurrentDirectory, "DicomStorage");
    public ObservableCollection<DicomFileInfo> DicomFiles { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        DicomFiles = new ObservableCollection<DicomFileInfo>();
        DataContext = this;  // 设置数据上下文
        CreateStorageDirectory();
    }

    private void CreateStorageDirectory()
    {
        if (!Directory.Exists(_storageFolder))
        {
            Directory.CreateDirectory(_storageFolder);
        }
    }

    private async void ImportDicom_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "DICOM Files (*.dcm)|*.dcm|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            foreach (string filename in openFileDialog.FileNames)
            {
                await ImportDicomFile(filename);
            }
        }
    }

    private async Task ImportDicomFile(string sourceFilePath)
    {
        try
        {
            var dicomFile = await DicomFile.OpenAsync(sourceFilePath);
            var dataset = dicomFile.Dataset;

            var fileInfo = new DicomFileInfo
            {
                PatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown"),
                PatientID = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "Unknown"),
                StudyDate = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "Unknown"),
                Modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, "Unknown"),
                FilePath = sourceFilePath
            };

            // 复制文件到存储目录
            string destinationPath = Path.Combine(_storageFolder, Path.GetFileName(sourceFilePath));
            if (!File.Exists(destinationPath))
            {
                File.Copy(sourceFilePath, destinationPath, true);
            }
            // 在UI线程上更新集合
            Application.Current.Dispatcher.Invoke(() =>
            {
                DicomFiles.Add(fileInfo);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入文件失败: {ex.Message}");
        }
    }

    private async void RefreshList_Click(object sender, RoutedEventArgs e)
    {
        RefreshList();
    }
    private async void RefreshList()
    {
        try
        {
            DicomFiles.Clear();
            var files = Directory.GetFiles(_storageFolder, "*.dcm");
            foreach (string file in files)
            {
                await ImportDicomFile(file);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新列表失败: {ex.Message}");
        }
    }
    private void StartServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 创建日志工厂
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            // 创建服务集合并配置服务
            var services = new ServiceCollection();
            services.AddFellowOakDicom();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // 构建服务提供者
            var serviceProvider = services.BuildServiceProvider();
            DicomSetupBuilder.UseServiceProvider(serviceProvider);

            // 获取服务器工厂
            var serverFactory = serviceProvider.GetRequiredService<IDicomServerFactory>();

            // 确保存储目录存在
            if (string.IsNullOrEmpty(_storageFolder))
            {
                _storageFolder = Path.Combine(Environment.CurrentDirectory, "DicomStorage");
            }
            Directory.CreateDirectory(_storageFolder);

            // 启动DICOM服务器
            _server = serverFactory.Create<DicomUnifiedProvider>(int.Parse(ServerPort.Text));
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化DICOM服务器失败: {ex.Message}");
        }
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _server.Stop();
        _server.Dispose();
    }
}

public class DicomFileInfo
{
    public string PatientName { get; set; } = string.Empty;
    public string PatientID { get; set; } = string.Empty;
    public string StudyDate { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}