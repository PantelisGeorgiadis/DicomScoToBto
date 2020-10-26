using CommandLine;
using Dicom;
using Dicom.Imaging;
using Dicom.Imaging.Codec;
using Dicom.IO;
using Dicom.IO.Buffer;
using Dicom.Log;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;

using LogLevel = NLog.LogLevel;
using LogManager = NLog.LogManager;

namespace DicomScoToBto
{
    /// <summary>
    /// This class defines the application's command line arguments
    /// </summary>
    class Options
    {
        /// <summary>
        /// The Hologic SCO input file
        /// </summary>
        [Option('i', "Input", Required = true, HelpText = "Input Hologic SCO file")]
        public String Input { get; set; }

        /// <summary>
        /// The DICOM BTO output file
        /// </summary>
        [Option('o', "Output", Required = true, HelpText = "Output DICOM BTO file")]
        public String Output { get; set; }
    }

    /// <summary>
    /// This program decodes a Hologic SCO dataset to a DICOM BTO dataset
    /// </summary>
    class Program
    {
        /// <summary>
        /// The entry point
        /// </summary>
        static void Main(string[] args)
        {
            // Initialize logging
            InitializeLogging();

            // Create the private Hologic tags and add them
            // to the default dictionary
            CreateHologicPrivateDictionary();

            // Register DICOM implementations
            RegisterDicomImplementations();

            // Parse input arguments and...
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    try
                    {
                        // ... do the actual decoding
                        DecodeScoToBto(o.Input, o.Output);
                    }
                    catch (Exception ex)
                    {
                        LogManager.GetCurrentClassLogger().Error(ex);
                    }
                });
        }

        /// <summary>
        /// Initializes logging to console
        /// </summary>
        static void InitializeLogging()
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget
            {
                Layout = "${message}"
            };
            config.AddTarget("consoleTarget", consoleTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Creates a custom private tag dictionary for the Hologic SCO datasets
        /// </summary>
        static void CreateHologicPrivateDictionary()
        {
            // Create the XML private dictionary string
            var privateXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                <dictionaries>
                <dictionary creator=""HOLOGIC, Inc."">
                    <tag group=""7E01"" element=""1010"" vr=""SQ"" vm=""1"">Full imaging resolution</tag>
                    <tag group=""7E01"" element=""1011"" vr=""SQ"" vm=""1"">Lower imaging resolution</tag>
                    <tag group=""7E01"" element=""1012"" vr=""OB"" vm=""1"">Imaging data</tag>
                </dictionary>
                </dictionaries>";

            // Load the custom dictionary into a stream...
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(privateXml);
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);

                    // ... and add it to the default dictionary
                    var reader =
                        new DicomDictionaryReader(DicomDictionary.Default, DicomDictionaryFormat.XML, stream);
                    reader.Process();
                }
            }
        }

        /// <summary>
        /// Registers DICOM implementations
        /// </summary>
        static void RegisterDicomImplementations()
        {
            // Register the custom JPEG-LS decoders
            TranscoderManager.SetImplementation(DicomJpegLsTranscoderManager.Instance);

            // Register the default desktop IO implementation
            IOManager.SetImplementation(DesktopIOManager.Instance);

            // Register the NLog-based logging implementation
            Dicom.Log.LogManager.SetImplementation(NLogManager.Instance);
        }

        /// <summary>
        /// Performs the actual decoding
        /// </summary>
        /// <param name="inputFile">The Hologic SCO input file</param>
        /// <param name="outputFile">The DICOM BTO output file</param>
        static void DecodeScoToBto(String inputFile, String outputFile)
        {
            // Check for a valid DICOM header
            if (!DicomFile.HasValidHeader(inputFile))
                throw new Exception("Input file doesn't seem to have a valid DICOM header");

            // Open the input file and get the DICOM dataset
            var dicomFile = DicomFile.Open(inputFile);
            var originalDataset = dicomFile.Dataset;

            // Get the private sequence tag that contains the full resolution pixels
            var privateTagFullRes = originalDataset.GetPrivateTag(new DicomTag(0x7E01, 0x1010, "HOLOGIC, Inc."));
            var privateTagFullResSequence = originalDataset.GetDicomItem<DicomSequence>(privateTagFullRes);
            if (privateTagFullResSequence == null ||
                privateTagFullResSequence.Items == null ||
                privateTagFullResSequence.Items.Count == 0)
                throw new Exception("Input file doesn't seem to contain the required private tags (0x7E01, 0x1010)");

            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);

            // Concatenate the pixel tags from the private sequence items
            foreach (var privateTagFullResSequenceItem in privateTagFullResSequence.Items)
            {
                var privateTagFullResDataTag =
                    privateTagFullResSequenceItem.GetPrivateTag(new DicomTag(0x7E01, 0x1012, "HOLOGIC, Inc."));
                var privateTagFullResData =
                    privateTagFullResSequenceItem.GetDicomItem<DicomOtherByte>(privateTagFullResDataTag);
                if (privateTagFullResData == null)
                    throw new Exception(
                        "Input file doesn't seem to contain the required private tags (0x7E01, 0x1012)");

                binaryWriter.Write(privateTagFullResData.Buffer.Data);
            }

            binaryWriter.Flush();
            var binaryReader = new BinaryReader(memoryStream);

            // Read the frame count from the private data (Offset: 20)
            binaryReader.BaseStream.Seek(20, SeekOrigin.Begin);
            var frameCount = binaryReader.ReadUInt16();

            // Read the columns from the private data (Offset: 24)
            binaryReader.BaseStream.Seek(24, SeekOrigin.Begin);
            var columns = binaryReader.ReadUInt16();

            // Read the rows from the private data (Offset: 28)
            binaryReader.BaseStream.Seek(28, SeekOrigin.Begin);
            var rows = binaryReader.ReadUInt16();

            // Read the bits stored from the private data (Offset: 32)
            binaryReader.BaseStream.Seek(32, SeekOrigin.Begin);
            var bitsStored = binaryReader.ReadByte();

            // Read the allowed lossy error from the private data (Offset: 36)
            binaryReader.BaseStream.Seek(36, SeekOrigin.Begin);
            var near = binaryReader.ReadByte();

            // Read the frame data indexes from the private data
            // The indexes are starting at 1024 bytes before the end of the private data
            var frameIndexes = new List<int>();
            binaryReader.BaseStream.Seek(binaryReader.BaseStream.Length - 1024, SeekOrigin.Begin);
            for (var i = 0; i < frameCount; i++)
                frameIndexes.Add(binaryReader.ReadInt32());
            frameIndexes.Add(binaryReader.ReadInt32());

            // Extract the frame data using the read indexes
            var frames = new List<byte[]>();
            for (var i = 0; i < frameCount; i++)
            {
                binaryReader.BaseStream.Seek(frameIndexes[i], SeekOrigin.Begin);
                var bytesToRead = frameIndexes[i + 1] - frameIndexes[i] - 1;
                var frameBytes = binaryReader.ReadBytes(bytesToRead);
                frames.Add(frameBytes);
            }

            // Dispose the readers/writers
            binaryReader.Dispose();
            binaryWriter.Dispose();
            memoryStream.Dispose();

            // Create a new dataset
            // In the case the allowed lossy error is zero create a lossless dataset
            var newDataset =
                new DicomDataset(
                    near == 0 ? DicomTransferSyntax.JPEGLSLossless : DicomTransferSyntax.JPEGLSNearLossless);

            // Copy all items excluding private tags and pixel data (if they exist)
            foreach (var dicomItem in originalDataset)
            {
                if (!dicomItem.Tag.IsPrivate &&
                    dicomItem.Tag != DicomTag.PixelData)
                    newDataset.Add(dicomItem);
            }

            // New SOP
            newDataset.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.BreastTomosynthesisImageStorage);
            newDataset.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUID.Generate());

            // New rendering params 
            newDataset.AddOrUpdate<ushort>(DicomTag.Columns, columns);
            newDataset.AddOrUpdate<ushort>(DicomTag.Rows, rows);
            newDataset.AddOrUpdate<ushort>(DicomTag.BitsAllocated, 16);
            newDataset.AddOrUpdate<ushort>(DicomTag.BitsStored, bitsStored);
            newDataset.AddOrUpdate<ushort>(DicomTag.HighBit, (ushort) (bitsStored - 1));
            newDataset.AddOrUpdate(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value);

            // New pixel data
            var pixelData = DicomPixelData.Create(newDataset, true);

            // Iterate through all frames, construct a new JPEG-LS frame
            // and append it as a pixel fragment in the new dataset
            for (var i = 0; i < frameCount; i++)
            {
                using (var frameMemoryStream = new MemoryStream())
                {
                    using (var frameBinaryWriter = new BinaryWriter(frameMemoryStream))
                    {
                        // Create the JPEG-LS header
                        // Start of image (SOI) marker
                        frameBinaryWriter.Write(new byte[] {0xFF, 0xD8});
                        // Start of JPEG-LS frame (SOF55) marker – marker segment follows
                        frameBinaryWriter.Write(new byte[] {0xFF, 0xF7});
                        // Length of marker segment = 11 bytes including the length field
                        frameBinaryWriter.Write(new byte[] {0x00, 0x0B});
                        // P = Precision
                        frameBinaryWriter.Write(bitsStored);
                        // Y = Number of lines (big endian)
                        frameBinaryWriter.Write(SwapBytes(rows));
                        // X = Number of columns (big endian)
                        frameBinaryWriter.Write(SwapBytes(columns));
                        // Nf = Number of components in the frame = 1
                        frameBinaryWriter.Write((byte) 0x01);
                        // C1 = Component ID = 1 (first and only component)
                        frameBinaryWriter.Write((byte) 0x01);
                        // Sub-sampling: H1 = 1, V1 = 1
                        frameBinaryWriter.Write((byte) 0x11);
                        // Tq1 = 0 (this field is always 0)
                        frameBinaryWriter.Write((byte) 0x00);
                        // Start of scan (SOS) marker
                        frameBinaryWriter.Write(new byte[] {0xFF, 0xDA});
                        // Length of marker segment = 8 bytes including the length field
                        frameBinaryWriter.Write(new byte[] {0x00, 0x08});
                        // Ns = Number of components for this scan = 1
                        frameBinaryWriter.Write((byte) 0x01);
                        // Ci = Component ID = 1
                        frameBinaryWriter.Write((byte) 0x01);
                        // Tm1 = Mapping table index = 0 (no mapping table)
                        frameBinaryWriter.Write((byte) 0x00);
                        // NEAR 
                        frameBinaryWriter.Write(near);
                        // ILV = 0 (interleave mode = non-interleaved)
                        frameBinaryWriter.Write((byte) 0x00);
                        // Al = 0, Ah = 0 (no point transform)
                        frameBinaryWriter.Write((byte) 0x00);

                        // Append the extracted frame data
                        // Frame data
                        frameBinaryWriter.Write(frames[i]);

                        // Close the JPEG-LS frame
                        // End of image (EOI) marker
                        frameBinaryWriter.Write(new byte[] {0xFF, 0xD9});
                        frameBinaryWriter.Flush();

                        var frameBytes = frameMemoryStream.ToArray();
                        // Add the fragment
                        pixelData.AddFrame(EvenLengthBuffer.Create(new MemoryByteBuffer(frameBytes)));
                    }
                }
            }

            // Decompress the new DICOM file to Explicit Little Endian
            // This step is performed because the resulting JPEG-LS codestream cannot be decoded
            // on several viewers (_validBits are equal to zero at the end of the decoding process, needs more investigation...)
            // For this reason, the application is performing the decompression, silencing the decoding errors
            // and producing valid multi-frame part10 DICOM files.
            var decompressedDataset = newDataset.Clone(DicomTransferSyntax.ExplicitVRLittleEndian);

            // Create a new DICOM file object from the decompress dataset
            var newDicomFile = new DicomFile(decompressedDataset);

            // Persist the decompressed DICOM file to disk
            newDicomFile.Save(outputFile);
        }

        /// <summary>
        /// Swaps the bytes of an unsigned short value
        /// </summary>
        /// <param name="u">The unsigned short to be swapped</param>
        /// <returns>The swapped unsigned short</returns>
        static ushort SwapBytes(ushort u)
        {
            return (ushort) ((ushort) ((u & 0xff) << 8) | ((u >> 8) & 0xff));
        }
    }
}
