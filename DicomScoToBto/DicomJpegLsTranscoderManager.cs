using Dicom.Imaging.Codec;
using System;

namespace DicomScoToBto
{
    /// <summary>
    /// Implementation of <see cref="TranscoderManager"/> for decoding JPEG-LS datasets
    /// </summary>
    public sealed class DicomJpegLsTranscoderManager : TranscoderManager
    {
        /// <summary>
        /// Singleton instance of the <see cref="DicomJpegLsTranscoderManager"/>
        /// </summary>
        public static readonly TranscoderManager Instance;

        /// <summary>
        /// Initializes the static fields of <see cref="DicomJpegLsTranscoderManager"/>
        /// </summary>
        static DicomJpegLsTranscoderManager()
        {
            Instance = new DicomJpegLsTranscoderManager();
        }

        /// <summary>
        /// Initializes an instance of <see cref="DicomJpegLsTranscoderManager"/>
        /// </summary>
        public DicomJpegLsTranscoderManager()
        {
            LoadCodecsImpl(null, null);
        }

        /// <summary>
        /// Implementation of method to load codecs
        /// </summary>
        /// <param name="path">Directory path to codec assemblies</param>
        /// <param name="search">Search pattern for codec assemblies</param>
        protected override void LoadCodecsImpl(String path, String search)
        {
            // Clear all loaded codecs
            Codecs.Clear();

            // Register the JPEG-LS near lossless decoder
            var jpegLsNearLosslessDecoder =
                (IDicomCodec) Activator.CreateInstance(typeof(DicomJpegLsNearLosslessDecoder));
            Codecs[jpegLsNearLosslessDecoder.TransferSyntax] = jpegLsNearLosslessDecoder;

            // Register the JPEG-LS lossless decoder
            var jpegLsLosslessDecoder = (IDicomCodec) Activator.CreateInstance(typeof(DicomJpegLsLosslessDecoder));
            Codecs[jpegLsLosslessDecoder.TransferSyntax] = jpegLsLosslessDecoder;
        }
    }
}
