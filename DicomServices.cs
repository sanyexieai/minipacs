using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace minipacs
{
    public class DicomUnifiedProvider : DicomService, 
        IDicomServiceProvider, 
        IDicomCEchoProvider,
        IDicomCStoreProvider,
        IDicomCFindProvider,
        IDicomCMoveProvider
    {
        private readonly ILogger _logger;
        private readonly string _storageFolder;

        public DicomUnifiedProvider(INetworkStream stream, Encoding fallbackEncoding, ILogger logger, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, logger, dependencies)
        {
            _logger = logger;
            _storageFolder = Path.Combine(Environment.CurrentDirectory, "DicomStorage");
        }

        #region C-ECHO
        public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
        {
            return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
        }
        #endregion

        #region C-STORE
        public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
        {
            try
            {
                var studyUid = request.Dataset.GetString(DicomTag.StudyInstanceUID);
                var seriesUid = request.Dataset.GetString(DicomTag.SeriesInstanceUID);
                var instanceUid = request.Dataset.GetString(DicomTag.SOPInstanceUID);

                // 创建存储路径
                var path = Path.Combine(_storageFolder, studyUid, seriesUid);
                Directory.CreateDirectory(path);

                var filePath = Path.Combine(path, $"{instanceUid}.dcm");
                await request.File.SaveAsync(filePath);

                return new DicomCStoreResponse(request, DicomStatus.Success);
            }
            catch (Exception)
            {
                return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
            }
        }
        #endregion

        #region C-FIND

        public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
        {
            // 验证查询级别
            if (request.Level != DicomQueryRetrieveLevel.Patient &&
                request.Level != DicomQueryRetrieveLevel.Study &&
                request.Level != DicomQueryRetrieveLevel.Series &&
                request.Level != DicomQueryRetrieveLevel.Image)
            {
                yield return new DicomCFindResponse(request, DicomStatus.QueryRetrieveUnableToProcess);
                yield break;
            }

            string[] files = await GetDicomFiles();
            if (files == null)
            {
                yield return new DicomCFindResponse(request, DicomStatus.QueryRetrieveUnableToProcess);
                yield break;
            }

            DicomDataset lastMatchedDataset = null;
            foreach (var file in files)
            {
                var response = await ProcessDicomFile(file, request);
                if (response != null)
                {
                    lastMatchedDataset = response.Dataset; // 保存最后一个匹配的数据集
                    yield return response;
                }
            }

            // 发送最终响应
            var finalResponse = new DicomCFindResponse(request, DicomStatus.Success);
            if (lastMatchedDataset != null)
            {
                // 使用最后一个匹配项的数据
                finalResponse.Dataset = lastMatchedDataset;
            }
            yield return finalResponse;
        }
        #endregion

        #region C-MOVE

        public async IAsyncEnumerable<DicomCMoveResponse> OnCMoveRequestAsync(DicomCMoveRequest request)
        {
            var matchingFiles = new List<string>();
            var files = Directory.GetFiles(_storageFolder, "*.dcm", SearchOption.AllDirectories);

            // 首先收集所有匹配的文件
            foreach (var file in files)
            {
                try
                {
                    var dicomFile = await DicomFile.OpenAsync(file);
                    if (MatchesQuery(request.Dataset, dicomFile.Dataset))
                    {
                        matchingFiles.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to open DICOM file: {file}", file);
                }
            }

            // 发送初始响应
            yield return new DicomCMoveResponse(request, DicomStatus.Pending)
            {
                Remaining = matchingFiles.Count,
                Completed = 0
            };

            var completed = 0;
            var failed = 0;

            // 处理每个匹配的文件
            foreach (var file in matchingFiles)
            {
                try
                {
                    // TODO: 实现实际的文件传输逻辑
                    completed++;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to move file: {file}", file);
                    failed++;
                }

                yield return new DicomCMoveResponse(request, DicomStatus.Pending)
                {
                    Remaining = matchingFiles.Count - completed - failed,
                    Completed = completed
                };
            }

            // 发送最终响应
            yield return new DicomCMoveResponse(request, DicomStatus.Success)
            {
                Remaining = 0,
                Completed = completed
            };
        }
        #endregion

        public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            _logger.LogInformation($"收到关联请求: {association.CallingAE} -> {association.CalledAE}");
            
            foreach (var pc in association.PresentationContexts)
            {
                pc.SetResult(DicomPresentationContextResult.Accept);
                _logger.LogInformation($"接受演示上下文: {pc.AbstractSyntax}");
            }
            
            return SendAssociationAcceptAsync(association);
        }

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            return SendAssociationReleaseResponseAsync();
        }

        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            _logger.LogWarning($"收到中止: 来源={source}, 原因={reason}");
        }

        public void OnConnectionClosed(Exception exception)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "连接关闭时发生错误");
            }
            else
            {
                _logger.LogInformation("连接正常关闭");
            }
        }

        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            throw new NotImplementedException();
        }
        private async Task<string[]> GetDicomFiles()
        {
            try
            {
                return Directory.GetFiles(_storageFolder, "*.dcm", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取DICOM文件列表失败");
                return null;
            }
        }

        private async Task<DicomCFindResponse> ProcessDicomFile(string file, DicomCFindRequest request)
        {
            try
            {
                var dataset = await DicomFile.OpenAsync(file);
                if (MatchesQuery(request.Dataset, dataset.Dataset))
                {
                    var response = new DicomCFindResponse(request, DicomStatus.Pending);
                    response.Dataset = CreateResponseDataset(request.Level, dataset.Dataset);
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理DICOM文件失败: {file}", file);
            }
            return null;
        }

        private DicomDataset CreateResponseDataset(DicomQueryRetrieveLevel level, DicomDataset source)
        {
            var response = new DicomDataset();

            // 添加基本患者信息（这些在所有级别都需要）
            AddIfExists(response, source, DicomTag.PatientName);
            AddIfExists(response, source, DicomTag.PatientID);
            AddIfExists(response, source, DicomTag.PatientBirthDate);

            // 添加研究相关信息
            AddIfExists(response, source, DicomTag.StudyInstanceUID);
            AddIfExists(response, source, DicomTag.StudyDate);
            AddIfExists(response, source, DicomTag.StudyDescription);
            AddIfExists(response, source, DicomTag.Modality);
            AddIfExists(response, source, DicomTag.NumberOfStudyRelatedSeries);
            AddIfExists(response, source, DicomTag.NumberOfStudyRelatedInstances);

            // 根据查询级别添加额外信息
            switch (level)
            {
                case DicomQueryRetrieveLevel.Series:
                    AddIfExists(response, source, DicomTag.SeriesInstanceUID);
                    AddIfExists(response, source, DicomTag.SeriesNumber);
                    AddIfExists(response, source, DicomTag.SeriesDescription);
                    break;

                case DicomQueryRetrieveLevel.Image:
                    AddIfExists(response, source, DicomTag.SOPInstanceUID);
                    AddIfExists(response, source, DicomTag.InstanceNumber);
                    AddIfExists(response, source, DicomTag.SOPClassUID);
                    break;
            }

            // 如果源数据集中没有某些必要的信息，添加默认值
            if (!response.Contains(DicomTag.PatientName))
                response.Add(DicomTag.PatientName, "Unknown");
            if (!response.Contains(DicomTag.PatientID))
                response.Add(DicomTag.PatientID, "Unknown");
            if (!response.Contains(DicomTag.StudyDate))
                response.Add(DicomTag.StudyDate, "Unknown");
            if (!response.Contains(DicomTag.Modality))
                response.Add(DicomTag.Modality, "Unknown");
            if (!response.Contains(DicomTag.NumberOfStudyRelatedSeries))
                response.Add(DicomTag.NumberOfStudyRelatedSeries, "0");
            if (!response.Contains(DicomTag.NumberOfStudyRelatedInstances))
                response.Add(DicomTag.NumberOfStudyRelatedInstances, "0");
            if (!response.Contains(DicomTag.StudyInstanceUID))
                response.Add(DicomTag.StudyInstanceUID, "");

            return response;
        }

        private void AddIfExists(DicomDataset target, DicomDataset source, DicomTag tag)
        {
            if (source.Contains(tag))
            {
                target.Add(tag, source.GetString(tag));
            }
        }

        private bool MatchesQuery(DicomDataset query, DicomDataset dataset)
        {
            // 检查是否所有查询条件都是空字符串
            bool allEmpty = true;
            foreach (var tag in query)
            {
                string value = query.GetString(tag.Tag);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    allEmpty = false;
                    break;
                }
            }

            // 如果所有条件都是空字符串，返回true表示匹配所有记录
            if (allEmpty)
                return true;

            // 如果有非空条件，进行正常匹配
            foreach (var tag in query)
            {
                var queryValue = query.GetString(tag.Tag);

                // 跳过空白字符串条件
                if (string.IsNullOrWhiteSpace(queryValue))
                    continue;

                // 检查标签是否存在
                if (dataset.Contains(tag.Tag))
                {
                    var datasetValue = dataset.GetString(tag.Tag);

                    // 检查是否匹配
                    if (!WildcardMatch(queryValue, datasetValue))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool WildcardMatch(string pattern, string value)
        {
            // 处理 DICOM 通配符
            pattern = pattern.Replace("*", ".*").Replace("?", ".");
            return System.Text.RegularExpressions.Regex.IsMatch(
                value ?? string.Empty,
                "^" + pattern + "$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

    }
} 