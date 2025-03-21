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
using System.Windows.Media;
using System.Linq;
using System.Windows.Controls;

namespace minipacs;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private IDicomServer _server;
    private IDicomClientFactory _clientFactory;
    private string _storageFolder = Path.Combine(Environment.CurrentDirectory, "DicomStorage");
    public ObservableCollection<DicomFileInfo> DicomFiles { get; set; }
    private GridLength _leftColumnWidth;
    private GridLength _rightColumnWidth;
    private ObservableCollection<StudyInfo> _studyList = new ObservableCollection<StudyInfo>();

    public MainWindow()
    {
        InitializeComponent();
        DicomFiles = new ObservableCollection<DicomFileInfo>();
        DataContext = this;  // 设置数据上下文
        CreateStorageDirectory();
        StudyListView.ItemsSource = _studyList; // 将ListView绑定到_studyList
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
    private async void StartServer_Click(object sender, RoutedEventArgs e)
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

            _clientFactory = serviceProvider.GetRequiredService<IDicomClientFactory>();
            // 更新状态指示灯为绿色
            ServerStatusLight.Fill = new SolidColorBrush(Colors.LimeGreen);
            // 启用推送配置
            PushConfigGrid.IsEnabled = true;
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化DICOM服务器失败: {ex.Message}");
            ServerStatusLight.Fill = new SolidColorBrush(Colors.Red);
        }
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _server?.Stop();
        _server?.Dispose();
        // 更新状态指示灯为红色
        ServerStatusLight.Fill = new SolidColorBrush(Colors.Red);
        // 禁用推送配置
        PushConfigGrid.IsEnabled = false;
    }

    private void ToggleLeftPanel_Click(object sender, RoutedEventArgs e)
    {
        if (RightColumn.Width.Value > 0)
        {
            // 保存当前宽度
            _rightColumnWidth = RightColumn.Width;
            // 收起面板
            RightColumn.Width = new GridLength(0);
            LeftPanelToggleButton.Content = "<";
        }
        else
        {
            // 展开面板
            RightColumn.Width = _rightColumnWidth;
            LeftPanelToggleButton.Content = ">";
        }
    }

    private void ToggleRightPanel_Click(object sender, RoutedEventArgs e)
    {
        if (LeftColumn.Width.Value > 0)
        {            // 保存当前宽度
            _leftColumnWidth = LeftColumn.Width;
            // 收起面板
            LeftColumn.Width = new GridLength(0);
            RightPanelToggleButton.Content = ">";

        }
        else
        {
            // 展开面板
            LeftColumn.Width = _leftColumnWidth;
            RightPanelToggleButton.Content = "<";
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var client = _clientFactory.Create(
                RemoteHost.Text,
                int.Parse(RemotePort.Text),
                false,
                RemoteAE.Text,
                RemoteCalledAE.Text);

            var request = new DicomCEchoRequest();
            await client.AddRequestAsync(request);
            await client.SendAsync();

            MessageBox.Show("连接成功！", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"连接失败: {ex.Message}", "错误");
        }
    }

    private async void Button_Click_1(object sender, RoutedEventArgs e)
    {
        try
        {
            _studyList.Clear(); // 清空之前的结果

            var client = _clientFactory.Create(
                RemoteHost.Text,
                int.Parse(RemotePort.Text),
                false,
                RemoteAE.Text,
                RemoteCalledAE.Text);

            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Study);

            // 添加查询条件
            request.Dataset.AddOrUpdate(DicomTag.PatientName, "*");
            request.Dataset.AddOrUpdate(DicomTag.PatientID, "*");
            request.Dataset.AddOrUpdate(DicomTag.StudyDate, "*");
            request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, "*");
            request.Dataset.AddOrUpdate(DicomTag.Modality, "*");
            request.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedSeries, "*");
            request.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedInstances, "*");
            request.Dataset.AddOrUpdate(DicomTag.StudyDescription, "*");

            request.OnResponseReceived = (request, response) =>
            {
                // 只处理 Pending 状态的响应，Success 状态通常是查询结束的标志
                if (response.Status == DicomStatus.Pending && response.Dataset != null)
                {
                    var studyInstanceUID = response.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, "");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 检查是否已存在相同的 StudyInstanceUID
                        if (!_studyList.Any(s => s.StudyInstanceUID == studyInstanceUID))
                        {
                            _studyList.Add(new StudyInfo
                            {
                                PatientName = response.Dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown"),
                                PatientID = response.Dataset.GetSingleValueOrDefault(DicomTag.PatientID, "Unknown"),
                                StudyDate = response.Dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "Unknown"),
                                Modality = response.Dataset.GetSingleValueOrDefault(DicomTag.Modality, "Unknown"),
                                SeriesCount = response.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfStudyRelatedSeries, "0"),
                                ImageCount = response.Dataset.GetSingleValueOrDefault(DicomTag.NumberOfStudyRelatedInstances, "0"),
                                StudyInstanceUID = studyInstanceUID
                            });
                        }
                    });
                }
            };

            await client.AddRequestAsync(request);
            await client.SendAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private void StudyListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 根据是否有选中项来显示或隐藏C-MOVE按钮
        CmoveButton.Visibility = StudyListView.SelectedItem != null ? 
            Visibility.Visible : Visibility.Collapsed;
    }

    private async void CmoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedStudy = StudyListView.SelectedItem as StudyInfo;
        if (selectedStudy == null) return;

        // 显示端口输入对话框
        var dialog = new MoveDestinationDialog();
        dialog.Owner = this;
        if (dialog.ShowDialog() != true) return;

        try
        {

            using (var server = DicomServerFactory.Create<DicomUnifiedProvider>(dialog.Port))
            {
                var client = _clientFactory.Create(
                RemoteHost.Text,
                int.Parse(RemotePort.Text), // C-MOVE端口
                false,
                RemoteAE.Text,
                RemoteCalledAE.Text);

                var request = new DicomCMoveRequest(
                     RemoteAE.Text,
                    selectedStudy.StudyInstanceUID);

                await client.AddRequestAsync(request);
                await client.SendAsync();
            }

            MessageBox.Show($"C-MOVE请求已发送\n接收端口：{dialog.Port}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"C-MOVE失败: {ex.Message}", "错误");
        }
    }

    // 添加属性来存储和获取推送配置
    public string CurrentPushHost
    {
        get
        {
            return Dispatcher.Invoke(() => PushHost.Text);
        }
    }

    public int CurrentPushPort
    {
        get
        {
            return Dispatcher.Invoke(() => int.Parse(PushPort.Text));
        }
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

public class StudyInfo
{
    public string PatientName { get; set; } = string.Empty;
    public string PatientID { get; set; } = string.Empty;
    public string StudyDate { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string SeriesCount { get; set; } = string.Empty;
    public string ImageCount { get; set; } = string.Empty;
    public string StudyInstanceUID { get; set; } = string.Empty;
}