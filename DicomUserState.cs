using System;
using System.Collections.Generic;
using FellowOakDicom;
using FellowOakDicom.Network;

namespace minipacs
{
    public class DicomUserState
    {
        public string AETitle { get; set; } = "MINIPACS";
        public int MaxAssociations { get; set; } = 10;
        public Dictionary<string, DicomTransferSyntax[]> TransferSyntaxes { get; private set; }

        public DicomUserState()
        {
            TransferSyntaxes = new Dictionary<string, DicomTransferSyntax[]>
            {
                {
                    DicomUID.Verification.UID,
                    new[] { DicomTransferSyntax.ExplicitVRLittleEndian }
                },
                {
                    DicomUID.CTImageStorage.UID,
                    new[]
                    {
                        DicomTransferSyntax.ExplicitVRLittleEndian,
                        DicomTransferSyntax.ExplicitVRBigEndian,
                        DicomTransferSyntax.ImplicitVRLittleEndian
                    }
                },
                // 添加其他存储类型的支持
                {
                    DicomUID.MRImageStorage.UID,
                    new[]
                    {
                        DicomTransferSyntax.ExplicitVRLittleEndian,
                        DicomTransferSyntax.ExplicitVRBigEndian,
                        DicomTransferSyntax.ImplicitVRLittleEndian
                    }
                },
                {
                    DicomUID.UltrasoundImageStorage.UID,
                    new[]
                    {
                        DicomTransferSyntax.ExplicitVRLittleEndian,
                        DicomTransferSyntax.ExplicitVRBigEndian,
                        DicomTransferSyntax.ImplicitVRLittleEndian
                    }
                },
                // 添加C-FIND支持的信息模型
                {
                    DicomUID.PatientRootQueryRetrieveInformationModelFind.UID,
                    new[] { DicomTransferSyntax.ExplicitVRLittleEndian }
                },
                {
                    DicomUID.StudyRootQueryRetrieveInformationModelFind.UID,
                    new[] { DicomTransferSyntax.ExplicitVRLittleEndian }
                },
                // 添加C-MOVE支持
                {
                    DicomUID.PatientRootQueryRetrieveInformationModelMove.UID,
                    new[] { DicomTransferSyntax.ExplicitVRLittleEndian }
                },
                {
                    DicomUID.StudyRootQueryRetrieveInformationModelMove.UID,
                    new[] { DicomTransferSyntax.ExplicitVRLittleEndian }
                },
                // 如果需要支持其他模型，可以继续添加
            };
        }
    }
} 