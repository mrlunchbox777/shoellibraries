using System;

namespace StandardDot.CoreServices.Manager
{
	public abstract class AFinalDispose : IFinalDispose
	{
		protected virtual string DisposedMessage => this?.GetType()?.FullName ?? "unknown " + nameof(AFinalDispose);

		protected virtual void ValidateDisposed()
		{
			if (Disposed)
			{
				throw new ObjectDisposedException(DisposedMessage);
			}
		}

		public virtual void Open()
		{
			if (Disposed)
			{
				GC.ReRegisterForFinalize(this);
				Disposed = false;
			}
		}

		public virtual void Close()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void Dispose()
		{
			Close();
		}
		
		protected bool Disposed { get; private set; }

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing || Disposed)
			{
				return;
			}
			Disposed = true;
		}

		protected virtual void Finalizer()
		{ }

		~AFinalDispose()
		{
			Dispose(false);
			Finalizer();
		}
	}
}