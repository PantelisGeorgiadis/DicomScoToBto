using CharLS;
using Dicom;
using Dicom.Imaging;
using Dicom.Imaging.Codec;
using Dicom.IO.Buffer;
using System;

namespace DicomScoToBto
{
    /// <summary>
    /// This class implements the JPEG-LS lossless decoder
    /// </summary>
    public class DicomJpegLsLosslessDecoder : DicomJpegLsDecoder
    {
        /// <summary>
        /// The implemented transfer syntax
        /// </summary>
        public override DicomTransferSyntax TransferSyntax
        {
            get { return DicomTransferSyntax.JPEGLSLossless; }
        }
    }

    /// <summary>
    /// This class implements the JPEG-LS near lossless decoder
    /// </summary>
    public class DicomJpegLsNearLosslessDecoder : DicomJpegLsDecoder
    {
        /// <summary>
        /// The implemented transfer syntax
        /// </summary>
        public override DicomTransferSyntax TransferSyntax
        {
            get { return DicomTransferSyntax.JPEGLSNearLossless; }
        }
    }

    /// <summary>
    /// Implementation of the JPEG-LS decoder using managed-only code
    /// </summary>
    public abstract class DicomJpegLsDecoder : DicomJpegLsCodec
    {
        /// <summary>
        /// The encoding function, which is not implemented, since this application is
        /// not encoding anything to JPEG-LS
        /// </summary>
        /// <param name="oldPixelData">The old pixel data</param>
        /// <param name="newPixelData">The new pixel data</param>
        /// <param name="parameters">The compression parameters</param>
        public override void Encode(DicomPixelData oldPixelData, DicomPixelData newPixelData,
            DicomCodecParams parameters)
        {
            throw new DicomCodecException(String.Format("Encoding of transfer syntax {0} is not implemented",
                TransferSyntax));
        }

        /// <summary>
        /// The JPEG-LS decoding function
        /// </summary>
        /// <param name="oldPixelData">The old pixel data</param>
        /// <param name="newPixelData">The new pixel data</param>
        /// <param name="parameters">The compression parameters</param>
        public override void Decode(DicomPixelData oldPixelData, DicomPixelData newPixelData,
            DicomCodecParams parameters)
        {
            for (var frame = 0; frame < oldPixelData.NumberOfFrames; frame++)
            {
                var jpegLsData = oldPixelData.GetFrame(frame);

                var message = String.Empty;
                var jpegLsParams = new JlsParameters();
                var frameData = new byte[newPixelData.UncompressedFrameSize];

                var err = JpegLs.Decode(frameData, jpegLsData.Data, jpegLsParams, out message);

                var buffer = frameData.Length >= 1 * 1024 * 1024 || oldPixelData.NumberOfFrames > 1
                    ? (IByteBuffer) new TempFileBuffer(frameData)
                    : new MemoryByteBuffer(frameData);
                newPixelData.AddFrame(EvenLengthBuffer.Create(buffer));
            }
        }
    }
}
