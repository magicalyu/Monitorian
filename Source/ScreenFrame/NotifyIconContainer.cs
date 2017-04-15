﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

using ScreenFrame.Helper;

namespace ScreenFrame
{
	public class NotifyIconContainer : IDisposable
	{
		#region Type

		private class NotifyIconWindowListener : NativeWindow
		{
			public static NotifyIconWindowListener Create(NotifyIconContainer container)
			{
				if (!NotifyIconHelper.TryGetNotifyIconWindow(container.NotifyIcon, out NativeWindow window) ||
					(window.Handle == IntPtr.Zero))
				{
					return null;
				}
				return new NotifyIconWindowListener(container, window);
			}

			private readonly NotifyIconContainer _container;

			private NotifyIconWindowListener(NotifyIconContainer container, NativeWindow window)
			{
				this._container = container;
				this.AssignHandle(window.Handle);
			}

			protected override void WndProc(ref Message m)
			{
				_container.WndProc(ref m);

				base.WndProc(ref m);
			}

			public void Close() => this.ReleaseHandle();
		}

		#endregion

		public NotifyIcon NotifyIcon { get; }

		private NotifyIconWindowListener _listener;

		public NotifyIconContainer()
		{
			NotifyIcon = new NotifyIcon();
			NotifyIcon.MouseClick += OnMouseClick;
			NotifyIcon.MouseDoubleClick += OnMouseDoubleClick;
		}

		public string Text
		{
			get { return NotifyIcon.Text; }
			set { NotifyIcon.Text = value; }
		}

		#region Icon

		private System.Drawing.Icon _icon;
		private DpiScale _dpi;

		public void ShowIcon(string iconPath, string iconText)
		{
			if (string.IsNullOrWhiteSpace(iconPath))
				throw new ArgumentNullException(nameof(iconPath));

			var iconResource = System.Windows.Application.GetResourceStream(new Uri(iconPath));
			if (iconResource != null)
			{
				using (var iconStream = iconResource.Stream)
				{
					var icon = new System.Drawing.Icon(iconStream);
					ShowIcon(icon, iconText);
				}
			}
		}

		public void ShowIcon(System.Drawing.Icon icon, string iconText)
		{
			this._icon = icon ?? throw new ArgumentNullException(nameof(icon));
			_dpi = VisualTreeHelperAddition.GetNotificationAreaDpi();
			Text = iconText;

			NotifyIcon.Icon = GetIcon(this._icon, _dpi);
			NotifyIcon.Visible = true;

			if (_listener == null)
			{
				_listener = NotifyIconWindowListener.Create(this);
			}
		}

		private const int WM_DPICHANGED = 0x02E0;

		protected virtual void WndProc(ref Message m)
		{
			switch (m.Msg)
			{
				case WM_DPICHANGED:
					var oldDpi = _dpi;
					_dpi = DpiScaleExtension.FromUInt((uint)m.WParam);
					if (!oldDpi.Equals(_dpi))
					{
						OnDpiChanged(oldDpi, _dpi);
					}
					m.Result = IntPtr.Zero;
					break;
			}
		}

		protected virtual void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
		{
			if (_icon != null)
			{
				NotifyIcon.Icon = GetIcon(_icon, newDpi);
			}
		}

		private static System.Drawing.Icon GetIcon(System.Drawing.Icon icon, DpiScale dpi)
		{
			var iconSize = GetIconSize(dpi);
			return new System.Drawing.Icon(icon, iconSize);
		}

		private const double Limit16 = 1.1; // Upper limit (110%) for 16x16
		private const double Limit32 = 2.0; // Upper limit (200%) for 32x32

		private static System.Drawing.Size GetIconSize(DpiScale dpi)
		{
			var factor = dpi.DpiScaleX;
			if (factor <= Limit16)
			{
				return new System.Drawing.Size(16, 16);
			}
			if (factor <= Limit32)
			{
				return new System.Drawing.Size(32, 32);
			}
			return new System.Drawing.Size(48, 48);
		}

		#endregion

		#region Click

		public event EventHandler MouseLeftButtonClick;
		public event EventHandler<Point> MouseRightButtonClick;

		private void OnMouseClick(object sender, MouseEventArgs e)
		{
			NotifyIconHelper.SetNotifyIconWindowForeground(NotifyIcon);

			if (e.Button == MouseButtons.Right)
			{
				if (NotifyIconHelper.TryGetNotifyIconClickedPoint(NotifyIcon, out Point point))
					MouseRightButtonClick?.Invoke(this, point);
			}
			else
			{
				MouseLeftButtonClick?.Invoke(this, null);
			}
		}

		private void OnMouseDoubleClick(object sender, MouseEventArgs e)
		{
			MouseLeftButtonClick?.Invoke(this, null);
		}

		#endregion

		#region IDisposable

		private bool _isDisposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_isDisposed)
				return;

			if (disposing)
			{
				_listener?.Close();
				NotifyIcon.Dispose();
			}

			_isDisposed = true;
		}

		#endregion
	}
}