using System.IO;
using Avalonia.Media.Imaging;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class ImageData
	{
		[Null]
		public global::Avalonia.Media.Imaging.Bitmap ImageSource { get; }

		public long FileSize { get; }

		public bool IsLfs { get; }

		public bool IsTracked { get; }

		public ImageData([Null] global::Avalonia.Media.Imaging.Bitmap imageSource, long fileSize, bool isLfs, bool isTracked)
		{
			global::Avalonia.Media.IImage = imageSource;
			FileSize = fileSize;
			IsLfs = isLfs;
			IsTracked = isTracked;
		}

		public static ImageData Create(MemoryStream memoryStream, bool isLfs, bool isTracked)
		{
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(memoryStream), memoryStream.Length, isLfs, isTracked);
		}

		public static ImageData Create(ImageContent imageContent)
		{
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(imageContent.Data), imageContent.Size.GetValueOrDefault(), isLfs: false, imageContent.IsTracked);
		}
	}
}
