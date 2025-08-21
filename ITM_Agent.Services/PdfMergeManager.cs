// ITM_Agent.Services/PdfMergeManager.cs
using ITM_Agent.Core;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ITM_Agent.Services
{
    /// <summary>
    /// 여러 이미지 파일을 단일 PDF 문서로 병합하는 기능을 제공하는 서비스입니다.
    /// iTextSharp 5.x 버전을 사용하여 PDF를 생성합니다.
    /// </summary>
    public class PdfMergeManager
    {
        private readonly ILogger _logger;

        public PdfMergeManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void MergeImagesToPdf(List<string> imagePaths, string outputPdfPath)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                _logger.LogDebug("[PdfMergeManager] No images provided to merge.");
                return;
            }

            try
            {
                string pdfDirectory = Path.GetDirectoryName(outputPdfPath);
                if (!Directory.Exists(pdfDirectory))
                {
                    Directory.CreateDirectory(pdfDirectory);
                }

                _logger.LogEvent($"[PdfMergeManager] Starting PDF merge for '{Path.GetFileName(outputPdfPath)}' with {imagePaths.Count} images.");

                // 1. 첫 번째 이미지로 Document의 크기 결정
                Image firstImage = Image.GetInstance(imagePaths[0]);
                Document document = new Document(firstImage); // 페이지 크기를 첫 이미지에 맞춤

                using (var stream = new FileStream(outputPdfPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    PdfWriter.GetInstance(document, stream);
                    document.Open();

                    foreach (string imgPath in imagePaths)
                    {
                        try
                        {
                            Image image = Image.GetInstance(imgPath);

                            // 페이지 크기 및 여백 설정
                            document.SetPageSize(image);
                            document.NewPage();

                            // 이미지를 페이지에 꽉 채우기
                            image.SetAbsolutePosition(0, 0);
                            image.ScaleToFit(document.PageSize.Width, document.PageSize.Height);

                            document.Add(image);
                        }
                        catch (Exception exImg)
                        {
                            _logger.LogError($"[PdfMergeManager] Could not process image '{imgPath}'. Error: {exImg.Message}");
                        }
                    }
                } // using이 끝나면 stream이 자동으로 닫힘

                _logger.LogEvent($"[PdfMergeManager] PDF file created successfully: {outputPdfPath}");

                // 병합 성공 후 원본 이미지 파일 삭제
                DeleteSourceFiles(imagePaths);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PdfMergeManager] PDF merge process failed for '{outputPdfPath}'. Error: {ex.Message}");
            }
        }

        private void DeleteSourceFiles(List<string> filesToDelete)
        {
            int deletedCount = 0;
            foreach (string filePath in filesToDelete)
            {
                if (DeleteFileWithRetry(filePath))
                {
                    deletedCount++;
                }
            }
            _logger.LogDebug($"[PdfMergeManager] Deleted {deletedCount} of {filesToDelete.Count} source image files.");
        }

        private bool DeleteFileWithRetry(string filePath, int maxRetries = 5, int delayMs = 300)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                        File.Delete(filePath);
                    }
                    return true;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[PdfMergeManager] Failed to delete file '{filePath}' after retries. Error: {ex.Message}");
                    return false;
                }
            }
            return false;
        }
    }
}
