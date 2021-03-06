#region License
/*
Copyright � Joan Charmant 2008-2009.
joan.charmant@gmail.com
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.

 */
#endregion

#region Using directives
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using AForge.Video.DirectShow;
using Kinovea.ScreenManager.Languages;
using Kinovea.ScreenManager.Properties;
using Kinovea.Services;

#endregion

namespace Kinovea.ScreenManager
{
	public partial class CaptureScreenUserInterface : UserControl, IFrameServerCaptureContainer
	{	
		#region Internal delegates for async methods
		private delegate void InitDecodingSize();
        private InitDecodingSize m_InitDecodingSize;
		#endregion
		
		#region Events
		public event EventHandler<DrawingEventArgs> DrawingAdded;
		#endregion
		
		#region Properties
		#endregion

		#region Members
		private ICaptureScreenUIHandler m_ScreenUIHandler;	// CaptureScreen seen trough a limited interface.
		private FrameServerCapture m_FrameServer;

		// General
		private bool m_bTryingToConnect;
		private int m_iDelay;
		// Image
		private bool m_bStretchModeOn;			// This is just a toggle to know what to do on double click.
		private bool m_bShowImageBorder;
		private static readonly Pen m_PenImageBorder = Pens.SteelBlue;
		
		// Keyframes, Drawings, etc.
		private AbstractDrawingTool m_ActiveTool;
		private DrawingToolPointer m_PointerTool;
		private bool m_bDocked = true;
		private bool m_bTextEdit;
		private Point m_DescaledMouse;    // The current mouse point expressed in the original image size coordinates.
		
		// Other
		private System.Windows.Forms.Timer m_DeselectionTimer = new System.Windows.Forms.Timer();
		private MessageToaster m_MessageToaster;
		private string m_LastSavedImage;
		private string m_LastSavedVideo;
		private FilenameHelper m_FilenameHelper = new FilenameHelper();
		
		#region Context Menus
		private ContextMenuStrip popMenu = new ContextMenuStrip();
		private ToolStripMenuItem mnuCamSettings = new ToolStripMenuItem();
		private ToolStripMenuItem mnuSavePic = new ToolStripMenuItem();
		private ToolStripMenuItem mnuCloseScreen = new ToolStripMenuItem();

		private ContextMenuStrip popMenuDrawings = new ContextMenuStrip();
		private ToolStripMenuItem mnuConfigureDrawing = new ToolStripMenuItem();
		private ToolStripMenuItem mnuConfigureOpacity = new ToolStripMenuItem();
		private ToolStripSeparator mnuSepDrawing = new ToolStripSeparator();
		private ToolStripSeparator mnuSepDrawing2 = new ToolStripSeparator();
		private ToolStripMenuItem mnuDeleteDrawing = new ToolStripMenuItem();
		
		private ContextMenuStrip popMenuMagnifier = new ContextMenuStrip();
		private List<ToolStripMenuItem> maginificationMenus = new List<ToolStripMenuItem>();
		private ToolStripMenuItem mnuMagnifierDirect = new ToolStripMenuItem();
		private ToolStripMenuItem mnuMagnifierQuit = new ToolStripMenuItem();
		#endregion

		ToolStripButton m_btnToolPresets;
		
		private SpeedSlider sldrDelay = new SpeedSlider();
		
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		#endregion

		#region Constructor
		public CaptureScreenUserInterface(FrameServerCapture _FrameServer, ICaptureScreenUIHandler _screenUIHandler)
		{
			log.Debug("Constructing the CaptureScreen user interface.");
			m_ScreenUIHandler = _screenUIHandler;
			m_FrameServer = _FrameServer;
			m_FrameServer.SetContainer(this);
			m_FrameServer.Metadata = new Metadata(null, null);
			
			// Initialize UI.
			InitializeComponent();
			UpdateDelayLabel();
			AddExtraControls();
			this.Dock = DockStyle.Fill;
			ShowHideResizers(false);
			InitializeDrawingTools();
			InitializeMetadata();
			BuildContextMenus();
			tmrCaptureDeviceDetector.Interval = CaptureScreen.HeartBeat;
			m_bDocked = true;
			
			InitializeCaptureFiles();
			m_MessageToaster = new MessageToaster(pbSurfaceScreen);
			
			// Delegates
			m_InitDecodingSize = new InitDecodingSize(InitDecodingSize_Invoked);
			
			m_DeselectionTimer.Interval = 3000;
			m_DeselectionTimer.Tick += new EventHandler(DeselectionTimer_OnTick);

			TryToConnect();
			tmrCaptureDeviceDetector.Start();
		}
		#endregion
		
		#region IFrameServerCaptureContainer implementation
		public void DoInvalidate()
		{
			pbSurfaceScreen.Invalidate();
		}
		public void DoInitDecodingSize()
		{
			BeginInvoke(m_InitDecodingSize);
		}
		private void InitDecodingSize_Invoked()
		{			
			m_PointerTool.SetImageSize(m_FrameServer.ImageSize);
			m_FrameServer.CoordinateSystem.Stretch = 1;
			m_bStretchModeOn = false;
			
			PanelCenter_Resize(null, EventArgs.Empty);
			
			// As a matter of fact we pass here at the first received frame.
			ShowHideResizers(true);
			UpdateFilenameLabel();
		}
		public void DisplayAsGrabbing(bool _bIsGrabbing)
		{
			if(_bIsGrabbing)
			{
				pbSurfaceScreen.Visible = true;
				btnGrab.Image = Kinovea.ScreenManager.Properties.Resources.capturepause5;	
			}
			else
			{
				btnGrab.Image = Kinovea.ScreenManager.Properties.Resources.capturegrab5;	
			}
		}
		public void DisplayAsRecording(bool _bIsRecording)
		{
			if(_bIsRecording)
        	{
        		btnRecord.Image = Kinovea.ScreenManager.Properties.Resources.control_recstop;
        		toolTips.SetToolTip(btnRecord, ScreenManagerLang.ToolTip_RecordStop);
        		ToastStartRecord();
        	}
        	else
        	{
				btnRecord.Image = Kinovea.ScreenManager.Properties.Resources.control_rec;        		
				toolTips.SetToolTip(btnRecord, ScreenManagerLang.ToolTip_RecordStart);
				ToastStopRecord();
        	}
		}
		public void AlertDisconnected()
		{
			ToastDisconnect();
			pbSurfaceScreen.Invalidate();
		}
		public void DoUpdateCapturedVideos()
		{
			// Update the list of Captured Videos.
			// Similar to OrganizeKeyframe in PlayerScreen.
			
			pnlThumbnails.Controls.Clear();
			
			if(m_FrameServer.RecentlyCapturedVideos.Count > 0)
			{
				int iPixelsOffset = 0;
				int iPixelsSpacing = 20;
				
				for(int i = m_FrameServer.RecentlyCapturedVideos.Count - 1; i >= 0; i--)
				{
				 	CapturedVideoBox box = new CapturedVideoBox(m_FrameServer.RecentlyCapturedVideos[i]);
					SetupDefaultThumbBox(box);
					
					// Finish the setup
					box.Left = iPixelsOffset + iPixelsSpacing;
					box.pbThumbnail.Image = m_FrameServer.RecentlyCapturedVideos[i].Thumbnail;
					box.CloseThumb += CapturedVideoBox_Close;
					box.LaunchVideo += CapturedVideoBox_LaunchVideo;
					
					iPixelsOffset += (iPixelsSpacing + box.Width);
					pnlThumbnails.Controls.Add(box);
				}
				
				DockKeyframePanel(false);
				pnlThumbnails.Refresh();
			}
			else
			{
				DockKeyframePanel(true);
			}

		}
		public void DoUpdateStatusBar()
		{
			m_ScreenUIHandler.ScreenUI_UpdateStatusBarAsked();
		}
		#endregion
		
		#region Public Methods
		public void DisplayAsActiveScreen(bool _bActive)
		{
			// Called from ScreenManager.
			ShowBorder(_bActive);
		}
		public void RefreshUICulture()
		{
			// Labels
			lblImageFile.Text = ScreenManagerLang.Capture_NextImage;
			lblVideoFile.Text = ScreenManagerLang.Capture_NextVideo;
			int maxRight = Math.Max(lblImageFile.Right, lblVideoFile.Right);
			tbImageFilename.Left = maxRight + 5;
			tbVideoFilename.Left = maxRight + 5;
			UpdateDelayLabel();
			
			ReloadTooltipsCulture();
			ReloadToolsCulture();
			ReloadMenusCulture();
			ReloadCapturedVideosCulture();			
			
			// Update the file naming.
			// By doing this we fix the naming for prefs change in free text (FT), in pattern, switch from FT to pattern,
			// switch from pattern to FT, no change in pattern.
			// but we loose any changes that have been done between the last saving and now. (no pref change in FT)
			InitializeCaptureFiles();
			
			// Refresh image to update grids colors, etc.
			pbSurfaceScreen.Invalidate();
			
			m_FrameServer.UpdateMemoryCapacity();
		}
		public bool OnKeyPress(Keys _keycode)
		{
			bool bWasHandled = false;
			
			if(tbImageFilename.Focused || tbVideoFilename.Focused)
			{
				return false;
			}
			
			// Method called from the Screen Manager's PreFilterMessage.
			switch (_keycode)
			{
				case Keys.Space:
				case Keys.Return:
					{
						if ((ModifierKeys & Keys.Control) == Keys.Control)
						{
							btnRecord_Click(null, EventArgs.Empty);
						}
						else
						{
							OnButtonGrab();
						}
						bWasHandled = true;
						break;
					}
				case Keys.Escape:
					{
						if(m_FrameServer.IsRecording)
						{
							btnRecord_Click(null, EventArgs.Empty);
						}
						DisablePlayAndDraw();
						pbSurfaceScreen.Invalidate();
						bWasHandled = true;
						break;
					}
				case Keys.Left:
				case Keys.Right:
					{
						sldrDelay_KeyDown(null, new KeyEventArgs(_keycode));
						bWasHandled = true;
						break;
					}
				case Keys.Add:
					{
						IncreaseDirectZoom();
						bWasHandled = true;
						break;
					}
				case Keys.Subtract:
					{
						// Decrease Zoom.
						DecreaseDirectZoom();
						bWasHandled = true;
						break;
					}
				case Keys.Delete:
					{
						// Remove selected Drawing
						// Note: Should only work if the Drawing is currently being moved...
						DeleteSelectedDrawing();
						bWasHandled = true;
						break;
					}
				default:
					break;
			}

			return bWasHandled;
		}
		public void FullScreen(bool _bFullScreen)
		{
		    if (_bFullScreen && !m_bStretchModeOn)
			{
				m_bStretchModeOn = true;
				StretchSqueezeSurface();
			    m_FrameServer.Metadata.ResizeFinished();
			    DoInvalidate();
			}
		}
		public void AddImageDrawing(string _filename, bool _bIsSvg)
		{
		    if(!m_FrameServer.IsConnected || !File.Exists(_filename))
		        return;
		    
		    m_FrameServer.Metadata.SelectedDrawingFrame = 0;
		    m_FrameServer.Metadata.AddImageDrawing(_filename, _bIsSvg, 0);
		    pbSurfaceScreen.Invalidate();
		}
		public void AddImageDrawing(Bitmap _bmp)
		{
			// Add an image drawing from a bitmap.
			// Mimick all the actions that are normally taken when we select a drawing tool and click on the image.
			if(!m_FrameServer.IsConnected)
			    return;
			
		    m_FrameServer.Metadata.SelectedDrawingFrame = 0;
		    m_FrameServer.Metadata.AddImageDrawing(_bmp, 0);
			pbSurfaceScreen.Invalidate();
		}
		public void BeforeClose()
		{
			// This screen is about to be closed.
			tmrCaptureDeviceDetector.Stop();
			tmrCaptureDeviceDetector.Dispose();
			PreferencesManager.Save();
		}
		#endregion
		
		#region Various Inits & Setups
		private void CaptureScreenUserInterface_Load(object sender, EventArgs e)
        {
        	m_ScreenUIHandler.ScreenUI_SetAsActiveScreen();
        }
		private void AddExtraControls()
		{
			// Add additional controls to the screen. This is needed due to some issue in SharpDevelop with custom controls.
			//(This method is hopefully temporary).
			panelVideoControls.Controls.Add(sldrDelay);
			
			//sldrDelay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			sldrDelay.BackColor = System.Drawing.Color.White;
        	//sldrDelay.Enabled = false;
        	sldrDelay.LargeChange = 1;
        	sldrDelay.Location = new System.Drawing.Point(lblDelay.Left + lblDelay.Width + 10, lblDelay.Top + 2);
        	sldrDelay.Maximum = 100;
        	sldrDelay.Minimum = 0;
        	sldrDelay.MinimumSize = new System.Drawing.Size(20, 10);
        	sldrDelay.Name = "sldrSpeed";
        	sldrDelay.Size = new System.Drawing.Size(150, 10);
        	sldrDelay.SmallChange = 1;
        	sldrDelay.StickyValue = -100;
        	sldrDelay.StickyMark = true;
        	sldrDelay.Value = 0;
        	sldrDelay.ValueChanged += sldrDelay_ValueChanged;
        	sldrDelay.KeyDown += new System.Windows.Forms.KeyEventHandler(sldrDelay_KeyDown);
		}
		private void InitializeDrawingTools()
        {
			m_PointerTool = new DrawingToolPointer();
			m_ActiveTool = m_PointerTool;
		
			// Tools buttons.
			EventHandler handler = new EventHandler(drawingTool_Click);
			
			// Pointer tool button
			AddToolButton(m_PointerTool, handler);
			stripDrawingTools.Items.Add(new ToolStripSeparator());
			
			AddToolButton(ToolManager.Label, handler);
			AddToolButton(ToolManager.Pencil, handler);
			AddToolButtonPosture(drawingTool_Click);
			AddToolButtonWithMenu(new AbstractDrawingTool[]{ToolManager.Line, ToolManager.Circle}, 0, drawingTool_Click);
			AddToolButton(ToolManager.Arrow, drawingTool_Click);
			AddToolButton(ToolManager.CrossMark, handler);
			AddToolButton(ToolManager.Angle, drawingTool_Click);
			AddToolButtonWithMenu(new AbstractDrawingTool[]{ToolManager.Grid, ToolManager.Plane}, 0, drawingTool_Click);
			
			AddToolButton(ToolManager.Magnifier, new EventHandler(btnMagnifier_Click));
			
			// Tool presets
			m_btnToolPresets = CreateToolButton();
        	m_btnToolPresets.Image = Resources.SwatchIcon3;
        	m_btnToolPresets.Click += btnColorProfile_Click;
        	m_btnToolPresets.ToolTipText = ScreenManagerLang.ToolTip_ColorProfile;
        	stripDrawingTools.Items.Add(m_btnToolPresets);
        }
		private ToolStripButton CreateToolButton()
		{
			ToolStripButton btn = new ToolStripButton();
			btn.AutoSize = false;
        	btn.DisplayStyle = ToolStripItemDisplayStyle.Image;
        	btn.ImageScaling = ToolStripItemImageScaling.None;
        	btn.Size = new Size(25, 25);
        	btn.AutoToolTip = false;
        	return btn;
		}
		private void AddToolButton(AbstractDrawingTool _tool, EventHandler _handler)
		{
			ToolStripButton btn = CreateToolButton();
        	btn.Image = _tool.Icon;
        	btn.Tag = _tool;
        	btn.Click += _handler;
        	btn.ToolTipText = _tool.DisplayName;
        	stripDrawingTools.Items.Add(btn);
		}
		private void AddToolButtonWithMenu(AbstractDrawingTool[] _tools, int selectedIndex, EventHandler _handler)
		{
		    // TODO:Deduplicate with PlayerScreen.
		    
		    // Adds a button with a sub menu.
		    // Each menu item will act as a button, and the master button will take the icon of the selected menu.
		    
		    ToolStripButtonWithDropDown btn = new ToolStripButtonWithDropDown();
			btn.AutoSize = false;
        	btn.DisplayStyle = ToolStripItemDisplayStyle.Image;
        	btn.ImageScaling = ToolStripItemImageScaling.None;
        	btn.Size = new Size(25, 25);
        	btn.AutoToolTip = false;

        	for(int i = _tools.Length-1;i>=0;i--)
        	{
        	    AbstractDrawingTool tool = _tools[i];
        	    ToolStripMenuItem item = new ToolStripMenuItem();
        	    item.Image = tool.Icon;
        	    item.Text = tool.DisplayName;
        	    item.Tag = tool;
        	    int indexClosure = _tools.Length - 1 - i;
        	    item.Click += (s,e) =>
        	    {
        	        btn.SelectedIndex = indexClosure;
        	        _handler(s,e);
        	    };

        	    btn.DropDownItems.Add(item);
        	}
        	
        	btn.SelectedIndex = _tools.Length - 1 - selectedIndex;
        	
        	stripDrawingTools.Items.Add(btn);
		}
		private void AddToolButtonPosture(EventHandler _handler)
        {
		    if(GenericPostureManager.Tools.Count > 0)
		        AddToolButtonWithMenu(GenericPostureManager.Tools.ToArray(), 0, drawingTool_Click);
		}
		private void InitializeMetadata()
		{
			// In capture, there is always a single keyframe.
			// All drawings are considered motion guides.
			Keyframe kf = new Keyframe(m_FrameServer.Metadata);
			kf.Position = 0;
			m_FrameServer.Metadata.Add(kf);
			
			// Check if there is a startup kva.
			// For capture, the kva will only work if the drawings are on a frame at position 0.
			string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Kinovea\\";
            string startupFile = folder + "\\capture.kva";
            if(File.Exists(startupFile))
                m_FrameServer.Metadata.Load(startupFile, true);
            
            // Strip extra keyframes, as there can only be one for capture.
            if(m_FrameServer.Metadata.Count > 1)
            {
                m_FrameServer.Metadata.Keyframes.RemoveRange(1, m_FrameServer.Metadata.Keyframes.Count - 1);
            }
		}
		private void ShowHideResizers(bool _bShow)
		{
			ImageResizerNE.Visible = _bShow;
			ImageResizerNW.Visible = _bShow;
			ImageResizerSE.Visible = _bShow;
			ImageResizerSW.Visible = _bShow;
		}
		private void BuildContextMenus()
		{
			// Attach the event handlers and build the menus.
			
			// 1. Default context menu.
			mnuCamSettings.Click += new EventHandler(btnCamSettings_Click);
			mnuCamSettings.Image = Properties.Resources.camera_video;
			mnuSavePic.Click += new EventHandler(btnSnapShot_Click);
			mnuSavePic.Image = Properties.Resources.picture_save;
			mnuCloseScreen.Click += new EventHandler(btnClose_Click);
			mnuCloseScreen.Image = Properties.Resources.capture_close2;
			popMenu.Items.AddRange(new ToolStripItem[] { mnuCamSettings, mnuSavePic, new ToolStripSeparator(), mnuCloseScreen });

			// 2. Drawings context menu (Configure, Delete, Track this)
			mnuConfigureDrawing.Click += new EventHandler(mnuConfigureDrawing_Click);
			mnuConfigureDrawing.Image = Properties.Drawings.configure;
			mnuConfigureOpacity.Click += new EventHandler(mnuConfigureOpacity_Click);
			mnuConfigureOpacity.Image = Properties.Drawings.persistence;
			mnuDeleteDrawing.Click += new EventHandler(mnuDeleteDrawing_Click);
			mnuDeleteDrawing.Image = Properties.Drawings.delete;

			// 5. Magnifier
			foreach(double factor in Magnifier.MagnificationFactors)
			    maginificationMenus.Add(CreateMagnificationMenu(factor));
			maginificationMenus[1].Checked = true;
			popMenuMagnifier.Items.AddRange(maginificationMenus.ToArray());
			
			mnuMagnifierDirect.Click += new EventHandler(mnuMagnifierDirect_Click);
			mnuMagnifierDirect.Image = Properties.Resources.arrow_out;
			mnuMagnifierQuit.Click += new EventHandler(mnuMagnifierQuit_Click);
			mnuMagnifierQuit.Image = Properties.Resources.hide;
			popMenuMagnifier.Items.AddRange(new ToolStripItem[] { new ToolStripSeparator(), mnuMagnifierDirect, mnuMagnifierQuit });
			
			// The right context menu and its content will be choosen upon MouseDown.
			panelCenter.ContextMenuStrip = popMenu;
			
			// Load texts
			ReloadMenusCulture();
		}
		private void InitializeCaptureFiles()
		{
			// Get the last values used and move forward (or use default if first time).
			m_LastSavedImage = m_FilenameHelper.InitImage();
			m_LastSavedVideo = m_FilenameHelper.InitVideo();
			tbImageFilename.Text = m_LastSavedImage;
			tbVideoFilename.Text = m_LastSavedVideo;
			tbImageFilename.Enabled = !PreferencesManager.CapturePreferences.CaptureUsePattern;
			tbVideoFilename.Enabled = !PreferencesManager.CapturePreferences.CaptureUsePattern;
		}
		private void UpdateFilenameLabel()
		{
			lblFileName.Text = m_FrameServer.DeviceName;
		}
		private ToolStripMenuItem CreateMagnificationMenu(double magnificationFactor)
		{
		    ToolStripMenuItem mnu = new ToolStripMenuItem();
			mnu.Tag = magnificationFactor;
			mnu.Text = String.Format(ScreenManagerLang.mnuMagnification, magnificationFactor.ToString());
			mnu.Click += mnuMagnifierChangeMagnification;
			return mnu;
		}
		#endregion
		
		#region Misc Events
		private void btnClose_Click(object sender, EventArgs e)
		{
			// Propagate to PlayerScreen which will report to ScreenManager.
			this.Cursor = Cursors.WaitCursor;
			m_ScreenUIHandler.ScreenUI_CloseAsked();
		}
		private void DeselectionTimer_OnTick(object sender, EventArgs e) 
		{
			// Deselect the currently selected drawing.
			// This is used for drawings that must show extra stuff for being transformed, but we 
			// don't want to show the extra stuff all the time for clarity.
			
			m_FrameServer.Metadata.UnselectAll();
			log.Debug("Deselection timer fired.");
			m_DeselectionTimer.Stop();
			pbSurfaceScreen.Invalidate();
		}
		#endregion
		
		#region Misc private helpers
		private void OnPoke()
		{
			//------------------------------------------------------------------------------
			// This function is a hub event handler for all button press, mouse clicks, etc.
			// Signal itself as the active screen to the ScreenManager
			//---------------------------------------------------------------------
			
			m_ScreenUIHandler.ScreenUI_SetAsActiveScreen();
			
			m_FrameServer.Metadata.AllDrawingTextToNormalMode();
			m_ActiveTool = m_ActiveTool.KeepToolFrameChanged ? m_ActiveTool : m_PointerTool;
			if(m_ActiveTool == m_PointerTool)
				SetCursor(m_PointerTool.GetCursor(-1));

			if (m_FrameServer.RecentlyCapturedVideos.Count < 1)
				DockKeyframePanel(true);
		}
		private void DoDrawingUndrawn()
		{
			//--------------------------------------------------------
			// this function is called after we undo a drawing action.
			// Called from CommandAddDrawing.Unexecute() through a delegate.
			//--------------------------------------------------------
			m_ActiveTool = m_ActiveTool.KeepToolFrameChanged ? m_ActiveTool : m_PointerTool;
			if(m_ActiveTool == m_PointerTool)
			{
				SetCursor(m_PointerTool.GetCursor(-1));
			}
		}
		private void ShowBorder(bool _bShow)
		{
			m_bShowImageBorder = _bShow;
			pbSurfaceScreen.Invalidate();
		}
		private void DrawImageBorder(Graphics _canvas)
		{
			// Draw the border around the screen to mark it as selected.
			// Called back from main drawing routine.
			_canvas.DrawRectangle(m_PenImageBorder, 0, 0, pbSurfaceScreen.Width - m_PenImageBorder.Width, pbSurfaceScreen.Height - m_PenImageBorder.Width);
		}
		private void DisablePlayAndDraw()
		{
			m_ActiveTool = m_PointerTool;
			SetCursor(m_PointerTool.GetCursor(0));
			DisableMagnifier();
			UnzoomDirectZoom();
		}
		#endregion
		
		#region Video Controls
		private void btnGrab_Click(object sender, EventArgs e)
		{
			if(m_FrameServer.IsConnected)
			{
				OnPoke();
				OnButtonGrab();
			}
			else
			{
				m_FrameServer.PauseGrabbing();	
			}
		}
		private void OnButtonGrab()
		{
			if(m_FrameServer.IsConnected)
			{
				if(m_FrameServer.IsGrabbing)
				{
					m_FrameServer.PauseGrabbing();
					ToastPause();
				}
			   	else
			   	{
					m_FrameServer.StartGrabbing();
			   	}
			}
		}
		public void Common_MouseWheel(object sender, MouseEventArgs e)
		{
			// MouseWheel was recorded on one of the controls.
			if(m_FrameServer.IsConnected)
			{
				int iScrollOffset = e.Delta * SystemInformation.MouseWheelScrollLines / 120;

				if ((ModifierKeys & Keys.Control) == Keys.Control)
				{
					if (iScrollOffset > 0)
					{
						IncreaseDirectZoom();
					}
					else
					{
						DecreaseDirectZoom();
					}
				}
				else
				{
					// return in recent frame history ?	
				}
			}
			
		}
		
		private void sldrDelay_ValueChanged(object sender, EventArgs e)
		{
			// sldrDelay value always goes [0..100].
			m_iDelay = m_FrameServer.DelayChanged(sldrDelay.Value);
			if(!m_FrameServer.IsGrabbing)
			{
				pbSurfaceScreen.Invalidate();	
			}
			UpdateDelayLabel();
		}
		private void sldrDelay_KeyDown(object sender, KeyEventArgs e)
		{
			// Increase/Decrease delay on LEFT/RIGHT Arrows.
			if (m_FrameServer.IsConnected)
			{				
				int jumpFactor = 25;
				if( (ModifierKeys & Keys.Control) == Keys.Control)
				{
					jumpFactor = 1;
				}
				else if((ModifierKeys & Keys.Shift) == Keys.Shift)
				{
					jumpFactor = 10;
				}
			
				if (e.KeyCode == Keys.Left)
				{
					sldrDelay.Value = jumpFactor * ((sldrDelay.Value-1) / jumpFactor);
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Right)
				{
					sldrDelay.Value = jumpFactor * ((sldrDelay.Value / jumpFactor) + 1);
					e.Handled = true;
				}
				
				m_iDelay = m_FrameServer.DelayChanged(sldrDelay.Value);
				if(!m_FrameServer.IsGrabbing)
				{
					pbSurfaceScreen.Invalidate();	
				}
				UpdateDelayLabel();
			}	
		}
		private void UpdateDelayLabel()
		{
			lblDelay.Text = String.Format(ScreenManagerLang.lblDelay_Text, m_iDelay);	
		}
		
		#endregion

		#region Auto Stretch & Manual Resize
		private void StretchSqueezeSurface()
		{
			if (m_FrameServer.IsConnected)
			{
				// Check if the image was loaded squeezed.
				// (happen when screen control isn't being fully expanded at video load time.)
				if(pbSurfaceScreen.Height < panelCenter.Height && m_FrameServer.CoordinateSystem.Stretch < 1.0)
				{
					m_FrameServer.CoordinateSystem.Stretch = 1.0;
				}
				
				Size imgSize = m_FrameServer.ImageSize;
				
				//---------------------------------------------------------------
				// Check if the stretch factor is not going to outsize the panel.
				// If so, force maximized, unless screen is smaller than video.
				//---------------------------------------------------------------
				int iTargetHeight = (int)((double)imgSize.Height * m_FrameServer.CoordinateSystem.Stretch);
				int iTargetWidth = (int)((double)imgSize.Width * m_FrameServer.CoordinateSystem.Stretch);
				
				if (iTargetHeight > panelCenter.Height || iTargetWidth > panelCenter.Width)
				{
					if (m_FrameServer.CoordinateSystem.Stretch > 1.0)
					{
						m_bStretchModeOn = true;
					}
				}
				
				if ((m_bStretchModeOn) || (imgSize.Width > panelCenter.Width) || (imgSize.Height > panelCenter.Height))
				{
					//-------------------------------------------------------------------------------
					// Maximiser :
					// Redimensionner l'image selon la dimension la plus proche de la taille du panel.
					//-------------------------------------------------------------------------------
					float WidthRatio = (float)imgSize.Width / panelCenter.Width;
					float HeightRatio = (float)imgSize.Height / panelCenter.Height;
					
					if (WidthRatio > HeightRatio)
					{
						pbSurfaceScreen.Width = panelCenter.Width;
						pbSurfaceScreen.Height = (int)((float)imgSize.Height / WidthRatio);
						
						m_FrameServer.CoordinateSystem.Stretch = (1 / WidthRatio);
					}
					else
					{
						pbSurfaceScreen.Width = (int)((float)imgSize.Width / HeightRatio);
						pbSurfaceScreen.Height = panelCenter.Height;
						
						m_FrameServer.CoordinateSystem.Stretch = (1 / HeightRatio);
					}
				}
				else
				{
					pbSurfaceScreen.Width = (int)((double)imgSize.Width * m_FrameServer.CoordinateSystem.Stretch);
					pbSurfaceScreen.Height = (int)((double)imgSize.Height * m_FrameServer.CoordinateSystem.Stretch);
				}
				
				// Center
				pbSurfaceScreen.Left = (panelCenter.Width / 2) - (pbSurfaceScreen.Width / 2);
				pbSurfaceScreen.Top = (panelCenter.Height / 2) - (pbSurfaceScreen.Height / 2);
				ReplaceResizers();
			}
		}
		private void ReplaceResizers()
		{
			ImageResizerSE.Left = pbSurfaceScreen.Left + pbSurfaceScreen.Width - (ImageResizerSE.Width / 2);
			ImageResizerSE.Top = pbSurfaceScreen.Top + pbSurfaceScreen.Height - (ImageResizerSE.Height / 2);

			ImageResizerSW.Left = pbSurfaceScreen.Left - (ImageResizerSW.Width / 2);
			ImageResizerSW.Top = pbSurfaceScreen.Top + pbSurfaceScreen.Height - (ImageResizerSW.Height / 2);

			ImageResizerNE.Left = pbSurfaceScreen.Left + pbSurfaceScreen.Width - (ImageResizerNE.Width / 2);
			ImageResizerNE.Top = pbSurfaceScreen.Top - (ImageResizerNE.Height / 2);

			ImageResizerNW.Left = pbSurfaceScreen.Left - (ImageResizerNW.Width / 2);
			ImageResizerNW.Top = pbSurfaceScreen.Top - (ImageResizerNW.Height / 2);
		}
		private void ToggleStretchMode()
		{
			if (!m_bStretchModeOn)
			{
				m_bStretchModeOn = true;
			}
			else
			{
				// Ne pas repasser en stretch mode � false si on est plus petit que l'image
				if (m_FrameServer.CoordinateSystem.Stretch >= 1)
				{
					m_FrameServer.CoordinateSystem.Stretch = 1;
					m_bStretchModeOn = false;
				}
			}
			StretchSqueezeSurface();
			m_FrameServer.Metadata.ResizeFinished();
			pbSurfaceScreen.Invalidate();
		}
		private void ImageResizerSE_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				int iTargetHeight = (ImageResizerSE.Top - pbSurfaceScreen.Top + e.Y);
				int iTargetWidth = (ImageResizerSE.Left - pbSurfaceScreen.Left + e.X);
				ResizeImage(iTargetWidth, iTargetHeight);
			}
		}
		private void ImageResizerSW_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				int iTargetHeight = (ImageResizerSW.Top - pbSurfaceScreen.Top + e.Y);
				int iTargetWidth = pbSurfaceScreen.Width + (pbSurfaceScreen.Left - (ImageResizerSW.Left + e.X));
				ResizeImage(iTargetWidth, iTargetHeight);
			}
		}
		private void ImageResizerNW_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				int iTargetHeight = pbSurfaceScreen.Height + (pbSurfaceScreen.Top - (ImageResizerNW.Top + e.Y));
				int iTargetWidth = pbSurfaceScreen.Width + (pbSurfaceScreen.Left - (ImageResizerNW.Left + e.X));
				ResizeImage(iTargetWidth, iTargetHeight);
			}
		}
		private void ImageResizerNE_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				int iTargetHeight = pbSurfaceScreen.Height + (pbSurfaceScreen.Top - (ImageResizerNE.Top + e.Y));
				int iTargetWidth = (ImageResizerNE.Left - pbSurfaceScreen.Left + e.X);
				ResizeImage(iTargetWidth, iTargetHeight);
			}
		}
		private void ResizeImage(int _iTargetWidth, int _iTargetHeight)
		{
			//-------------------------------------------------------------------
			// Resize at the following condition:
			// Bigger than original image size, smaller than panel size.
			//-------------------------------------------------------------------
			if (_iTargetHeight > m_FrameServer.ImageSize.Height &&
			    _iTargetHeight < panelCenter.Height &&
			    _iTargetWidth > m_FrameServer.ImageSize.Width &&
			    _iTargetWidth < panelCenter.Width)
			{
				double fHeightFactor = ((_iTargetHeight) / (double)m_FrameServer.ImageSize.Height);
				double fWidthFactor = ((_iTargetWidth) / (double)m_FrameServer.ImageSize.Width);

				m_FrameServer.CoordinateSystem.Stretch = (fWidthFactor + fHeightFactor) / 2;
				m_bStretchModeOn = false;
				StretchSqueezeSurface();
				pbSurfaceScreen.Invalidate();
			}
		}
		private void Resizers_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			ToggleStretchMode();
		}
		private void Resizers_MouseUp(object sender, MouseEventArgs e)
		{
			m_FrameServer.Metadata.ResizeFinished();
			pbSurfaceScreen.Invalidate();
		}
		#endregion
		
		#region Culture
		private void ReloadMenusCulture()
		{
			// Reload the text for each menu.
			// this is done at construction time and at RefreshUICulture time.
			
			// 1. Default context menu.
			mnuCamSettings.Text = ScreenManagerLang.ToolTip_DevicePicker;
			mnuSavePic.Text = ScreenManagerLang.Generic_SaveImage;
			mnuCloseScreen.Text = ScreenManagerLang.mnuCloseScreen;
			
			// 2. Drawings context menu.
			mnuConfigureDrawing.Text = ScreenManagerLang.mnuConfigureDrawing_ColorSize;
			mnuConfigureOpacity.Text = ScreenManagerLang.Generic_Opacity;
			mnuDeleteDrawing.Text = ScreenManagerLang.mnuDeleteDrawing;
			
			// 5. Magnifier
			foreach(ToolStripMenuItem m in maginificationMenus)
			{
			    double factor = (double)m.Tag;
			    m.Text = String.Format(ScreenManagerLang.mnuMagnification, factor.ToString());
			}
			mnuMagnifierDirect.Text = ScreenManagerLang.mnuMagnifierDirect;	
			mnuMagnifierQuit.Text = ScreenManagerLang.mnuMagnifierQuit;
		}
		private void ReloadTooltipsCulture()
		{
			// Video controls
			toolTips.SetToolTip(btnGrab, ScreenManagerLang.ToolTip_Play);
			toolTips.SetToolTip(btnCamSnap, ScreenManagerLang.Generic_SaveImage);
			toolTips.SetToolTip(btnCamSettings, ScreenManagerLang.ToolTip_DevicePicker);
			toolTips.SetToolTip(btnRecord, m_FrameServer.IsRecording ? ScreenManagerLang.ToolTip_RecordStop : ScreenManagerLang.ToolTip_RecordStart);
		}
		private void ReloadToolsCulture()
		{
			foreach(ToolStripItem tsi in stripDrawingTools.Items)
			{
			    if(tsi is ToolStripSeparator)
			        continue;
			    
			    if(tsi is ToolStripButtonWithDropDown)
			    {
                    foreach(ToolStripItem subItem in ((ToolStripButtonWithDropDown)tsi).DropDownItems)
				    {
                        if(!(subItem is ToolStripMenuItem))
                            continue;
				        
                        AbstractDrawingTool tool = subItem.Tag as AbstractDrawingTool;
				        if(tool != null)
				        {
				            subItem.Text = tool.DisplayName;
				            subItem.ToolTipText = tool.DisplayName;
				        }
				    }
                    
                    ((ToolStripButtonWithDropDown)tsi).UpdateToolTip();
			    }
				else if(tsi is ToolStripButton)
				{
					AbstractDrawingTool tool = tsi.Tag as AbstractDrawingTool;
					if(tool != null)
						tsi.ToolTipText = tool.DisplayName;
				}
			}
		
			m_btnToolPresets.ToolTipText = ScreenManagerLang.ToolTip_ColorProfile;
		}
		private void ReloadCapturedVideosCulture()
		{
			foreach(Control c in pnlThumbnails.Controls)
			{
				CapturedVideoBox cvb = c as CapturedVideoBox;
				if(cvb != null)
				{
					cvb.RefreshUICulture();
				}
			}
		}
		#endregion

		#region SurfaceScreen Events
		private void SurfaceScreen_MouseDown(object sender, MouseEventArgs e)
		{
			if(!m_FrameServer.IsConnected)
			    return;
			
			m_DeselectionTimer.Stop();
			m_DescaledMouse = m_FrameServer.CoordinateSystem.Untransform(e.Location);
				
			if (e.Button == MouseButtons.Left)
			    SurfaceScreen_LeftDown();
			else if (e.Button == MouseButtons.Right)
			    SurfaceScreen_RightDown();
				
			pbSurfaceScreen.Invalidate();
		}
		private void SurfaceScreen_LeftDown()
		{
		    bool hitMagnifier = false;
		    if(m_ActiveTool == m_PointerTool)
                hitMagnifier = m_FrameServer.Metadata.Magnifier.OnMouseDown(m_DescaledMouse, m_FrameServer.Metadata.CoordinateSystem);
		    
		    if(hitMagnifier)
		        return;
		    
			m_FrameServer.Metadata.AllDrawingTextToNormalMode();
		
			if (m_ActiveTool == m_PointerTool)
			{
				bool bDrawingHit = false;
			
				// Show the grabbing hand cursor.
				SetCursor(m_PointerTool.GetCursor(1));
				bDrawingHit = m_PointerTool.OnMouseDown(m_FrameServer.Metadata, 0, m_DescaledMouse, 0, PreferencesManager.PlayerPreferences.DefaultFading.Enabled);
			}
			else
			{
			    CreateNewDrawing();
			}
		}
		private void CreateNewDrawing()
		{
		    // TODO: deduplicate with PlayerScreenUI.
		    
		    if (m_ActiveTool != ToolManager.Label)
			{
				AbstractDrawing drawing = m_ActiveTool.GetNewDrawing(m_DescaledMouse, 0, 1);
				
				if(DrawingAdded != null)
				    DrawingAdded(this, new DrawingEventArgs(drawing, 0));
			}
			else
			{
				
				// We are using the Text Tool. This is a special case because
				// if we are on an existing Textbox, we just go into edit mode
				// otherwise, we add and setup a new textbox.
				bool bEdit = false;
				foreach (AbstractDrawing drawing in m_FrameServer.Metadata[0].Drawings)
				{
					if (drawing is DrawingText)
					{
						int hitRes = drawing.HitTest(m_DescaledMouse, 0, m_FrameServer.Metadata.CoordinateSystem);
						if (hitRes >= 0)
						{
							bEdit = true;
							((DrawingText)drawing).SetEditMode(true, m_FrameServer.CoordinateSystem);
						}
					}
				}
				
				// If we are not on an existing textbox : create new DrawingText.
				if (!bEdit)
				{
				    AbstractDrawing drawing = m_ActiveTool.GetNewDrawing(m_DescaledMouse, 0, 1);
				    
				    if(DrawingAdded != null)
				        DrawingAdded(this, new DrawingEventArgs(drawing, 0));
				    
					DrawingText drawingText = drawing as DrawingText;
					
					drawingText.ContainerScreen = pbSurfaceScreen;
					drawingText.SetEditMode(true, m_FrameServer.CoordinateSystem);
					TextBox textBox = drawingText.EditBox;
					panelCenter.Controls.Add(textBox);
					textBox.BringToFront();
					textBox.Focus();
				}
			}
		}
		private void SurfaceScreen_RightDown()
		{
		    // Show the right Pop Menu depending on context.
			// (Drawing, Magnifier, Nothing)
			
			m_FrameServer.Metadata.UnselectAll();
				
			if (m_FrameServer.Metadata.IsOnDrawing(0, m_DescaledMouse, 0))
			{
				// Rebuild the context menu according to the capabilities of the drawing we are on.
					
				AbstractDrawing ad = m_FrameServer.Metadata.Keyframes[m_FrameServer.Metadata.SelectedDrawingFrame].Drawings[m_FrameServer.Metadata.SelectedDrawing];
				if(ad != null)
				{
					popMenuDrawings.Items.Clear();
					
					// Generic context menu from drawing capabilities.
					if((ad.Caps & DrawingCapabilities.ConfigureColor) == DrawingCapabilities.ConfigureColor)
					{
					   	mnuConfigureDrawing.Text = ScreenManagerLang.mnuConfigureDrawing_Color;
					   	popMenuDrawings.Items.Add(mnuConfigureDrawing);
					}
					   
					if((ad.Caps & DrawingCapabilities.ConfigureColorSize) == DrawingCapabilities.ConfigureColorSize)
					{
						mnuConfigureDrawing.Text = ScreenManagerLang.mnuConfigureDrawing_ColorSize;
					   	popMenuDrawings.Items.Add(mnuConfigureDrawing);
					}
					
					if((ad.Caps & DrawingCapabilities.Opacity) == DrawingCapabilities.Opacity)
					{
						popMenuDrawings.Items.Add(mnuConfigureOpacity);
					}
					
					popMenuDrawings.Items.Add(mnuSepDrawing);

					// Specific menus. Hosted by the drawing itself.
					bool hasExtraMenu = (ad.ContextMenu != null && ad.ContextMenu.Count > 0);
					if(hasExtraMenu)
					{
						foreach(ToolStripMenuItem tsmi in ad.ContextMenu)
						{
							popMenuDrawings.Items.Add(tsmi);
						}
					}
					
					if(hasExtraMenu)
						popMenuDrawings.Items.Add(mnuSepDrawing2);
						
					// Generic delete
					popMenuDrawings.Items.Add(mnuDeleteDrawing);
					
					// Set this menu as the context menu.
					panelCenter.ContextMenuStrip = popMenuDrawings;
				}
			}
			else if (m_FrameServer.Magnifier.Mode == MagnifierMode.Indirect && m_FrameServer.Magnifier.IsOnObject(m_DescaledMouse, m_FrameServer.Metadata.CoordinateSystem))
			{
				panelCenter.ContextMenuStrip = popMenuMagnifier;
			}
			else if(m_ActiveTool != m_PointerTool)
			{
				// Launch FormToolPreset.
				FormToolPresets ftp = new FormToolPresets(m_ActiveTool);
				FormsHelper.Locate(ftp);
				ftp.ShowDialog();
				ftp.Dispose();
				UpdateCursor();
			}
			else
			{
				// No drawing touched and no tool selected
				panelCenter.ContextMenuStrip = popMenu;
			}
		}
		private void SurfaceScreen_MouseMove(object sender, MouseEventArgs e)
		{
			// We must keep the same Z order.
			// 1:Magnifier, 2:Drawings, 3:Chronos/Tracks
			// When creating a drawing, the active tool will stay on this drawing until its setup is over.
			// After the drawing is created, we either fall back to Pointer tool or stay on the same tool.
			
			if(!m_FrameServer.IsConnected)
			    return;
			
			m_DescaledMouse = m_FrameServer.CoordinateSystem.Untransform(e.Location);
			
			if (e.Button == MouseButtons.None && m_FrameServer.Magnifier.Mode == MagnifierMode.Direct)
			{
				//m_FrameServer.Magnifier.MouseX = e.X;
				//m_FrameServer.Magnifier.MouseY = e.Y;
				m_FrameServer.Magnifier.Move(m_DescaledMouse);
				pbSurfaceScreen.Invalidate();
			}
			else if (e.Button == MouseButtons.Left)
			{
				if (m_ActiveTool != m_PointerTool)
				{
					// Currently setting the second point of a Drawing.
					IInitializable initializableDrawing = m_FrameServer.Metadata[0].Drawings[0] as IInitializable;
					if(initializableDrawing != null)
						initializableDrawing.ContinueSetup(m_DescaledMouse, ModifierKeys);
				}
				else
				{
					bool bMovingMagnifier = false;
					if (m_FrameServer.Magnifier.Mode == MagnifierMode.Indirect)
					{
						bMovingMagnifier = m_FrameServer.Magnifier.Move(m_DescaledMouse);
					}
					
					if (!bMovingMagnifier && m_ActiveTool == m_PointerTool)
					{
						// Magnifier is not being moved or is invisible, try drawings through pointer tool.
						bool bMovingObject = m_PointerTool.OnMouseMove(m_FrameServer.Metadata, m_DescaledMouse, m_FrameServer.CoordinateSystem.Location, ModifierKeys);
						
						if (!bMovingObject && m_FrameServer.CoordinateSystem.Zooming)
						{
							// User is not moving anything and we are zooming : move the zoom window.
							
							// Get mouse deltas (descaled=in image coords).
							double fDeltaX = (double)m_PointerTool.MouseDelta.X;
							double fDeltaY = (double)m_PointerTool.MouseDelta.Y;
							
							m_FrameServer.CoordinateSystem.MoveZoomWindow(fDeltaX, fDeltaY);
						}
					}
				}
			}
				
			if (!m_FrameServer.IsGrabbing)
			{
				pbSurfaceScreen.Invalidate();
			}
		}
		private void SurfaceScreen_MouseUp(object sender, MouseEventArgs e)
		{
			// End of an action.
			// Depending on the active tool we have various things to do.
			
			if(!m_FrameServer.IsConnected || e.Button != MouseButtons.Left)
			    return;
			
			m_DescaledMouse = m_FrameServer.CoordinateSystem.Untransform(e.Location);
			
			if (m_ActiveTool == m_PointerTool)
				OnPoke();
			
			m_FrameServer.Magnifier.OnMouseUp(m_DescaledMouse);
			
			// Memorize the action we just finished to enable undo.
			if (m_ActiveTool != m_PointerTool)
			{
				// Record the adding unless we are editing a text box.
				if (!m_bTextEdit)
				{
					IUndoableCommand cad = new CommandAddDrawing(DoInvalidate, DoDrawingUndrawn, m_FrameServer.Metadata, m_FrameServer.Metadata[0].Position);
					CommandManager cm = CommandManager.Instance();
					cm.LaunchUndoableCommand(cad);
					
				}
				else
				{
					m_bTextEdit = false;
				}
			}
			
			// The fact that we stay on this tool or fall back to pointer tool, depends on the tool.
			m_ActiveTool = m_ActiveTool.KeepTool ? m_ActiveTool : m_PointerTool;
			
			if (m_ActiveTool == m_PointerTool)
			{
				SetCursor(m_PointerTool.GetCursor(0));
				m_PointerTool.OnMouseUp();
			
				// If we were resizing an SVG drawing, trigger a render.
				// TODO: this is currently triggered on every mouse up, not only on resize !
				int selectedFrame = m_FrameServer.Metadata.SelectedDrawingFrame;
				int selectedDrawing = m_FrameServer.Metadata.SelectedDrawing;
				if(selectedFrame != -1 && selectedDrawing  != -1)
				{
					DrawingSVG d = m_FrameServer.Metadata.Keyframes[selectedFrame].Drawings[selectedDrawing] as DrawingSVG;
					if(d != null)
					{
						d.ResizeFinished();
					}
				}
			
			}
						
			if (m_FrameServer.Metadata.SelectedDrawingFrame != -1 && m_FrameServer.Metadata.SelectedDrawing != -1)
			{
				m_DeselectionTimer.Start();					
			}
			
			pbSurfaceScreen.Invalidate();
		}
		private void SurfaceScreen_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if(!m_FrameServer.IsConnected || e.Button != MouseButtons.Left || m_ActiveTool == m_PointerTool)
			    return;
			
			OnPoke();
			
			m_DescaledMouse = m_FrameServer.CoordinateSystem.Untransform(e.Location);
			m_FrameServer.Metadata.AllDrawingTextToNormalMode();
			m_FrameServer.Metadata.UnselectAll();
			
			//------------------------------------------------------------------------------------
			// - If on text, switch to edit mode.
			// - If on other drawing, launch the configuration dialog.
			// - Otherwise -> Maximize/Reduce image.
			//------------------------------------------------------------------------------------
			if (m_FrameServer.Metadata.IsOnDrawing(0, m_DescaledMouse, 0))
			{
				AbstractDrawing ad = m_FrameServer.Metadata.Keyframes[0].Drawings[m_FrameServer.Metadata.SelectedDrawing];
				if (ad is DrawingText)
				{
					((DrawingText)ad).SetEditMode(true, m_FrameServer.CoordinateSystem);
					m_ActiveTool = ToolManager.Label;
					m_bTextEdit = true;
				}
				else if(ad is DrawingSVG || ad is DrawingBitmap)
				{
					mnuConfigureOpacity_Click(null, EventArgs.Empty);
				}
				else
				{
					mnuConfigureDrawing_Click(null, EventArgs.Empty);
				}
			}
			else
			{
				ToggleStretchMode();
			}
		}
		private void SurfaceScreen_Paint(object sender, PaintEventArgs e)
		{
			// Draw the image.
			m_FrameServer.Draw(e.Graphics);
			
			if(m_MessageToaster.Enabled)
			{
				m_MessageToaster.Draw(e.Graphics);
			}

			// Draw selection Border if needed.
			if (m_bShowImageBorder)
			{
				DrawImageBorder(e.Graphics);
			}	
		}
		private void SurfaceScreen_MouseEnter(object sender, EventArgs e)
		{
			
			// Set focus to surfacescreen to enable mouse scroll
			
			// But only if there is no Text edition going on.
			bool bEditing = false;
			
			foreach (AbstractDrawing ad in m_FrameServer.Metadata[0].Drawings)
			{
				DrawingText dt = ad as DrawingText;
				if (dt != null)
				{
					if(dt.EditMode)
					{
						bEditing = true;
						break;
					}
				}
			}
			
			
			if(!bEditing)
			{
				pbSurfaceScreen.Focus();
			}
		}
		#endregion

		#region PanelCenter Events
		private void PanelCenter_MouseEnter(object sender, EventArgs e)
		{
			// Give focus to enable mouse scroll.
			panelCenter.Focus();
		}
		private void PanelCenter_MouseClick(object sender, MouseEventArgs e)
		{
			OnPoke();
		}
		private void PanelCenter_Resize(object sender, EventArgs e)
		{
			StretchSqueezeSurface();
			pbSurfaceScreen.Invalidate();
		}
		private void PanelCenter_MouseDown(object sender, MouseEventArgs e)
		{
			panelCenter.ContextMenuStrip = popMenu;
		}
		#endregion
		
		#region Keyframes Panel
		private void pnlThumbnails_MouseEnter(object sender, EventArgs e)
		{
			// Give focus to disable keyframe box editing.
			pnlThumbnails.Focus();
		}
		private void splitKeyframes_Resize(object sender, EventArgs e)
		{
			// Redo the dock/undock if needed to be at the right place.
			// (Could be handled by layout ?)
			DockKeyframePanel(m_bDocked);
		}
		private void SetupDefaultThumbBox(UserControl _box)
		{
			_box.Top = 10;
			_box.Cursor = Cursors.Hand;
		}
		public void OnKeyframesTitleChanged()
		{
			// Called when title changed.
			pbSurfaceScreen.Invalidate();
		}
		private void pnlThumbnails_DoubleClick(object sender, EventArgs e)
		{
			OnPoke();
		}
		private void CapturedVideoBox_LaunchVideo(object sender, EventArgs e)
		{
			CapturedVideoBox box = sender as CapturedVideoBox;
			if(box != null)
			{
				m_ScreenUIHandler.CaptureScreenUI_LoadVideo(box.FilePath);
			}
		}
		private void CapturedVideoBox_Close(object sender, EventArgs e)
		{
			CapturedVideoBox box = sender as CapturedVideoBox;
			if(box != null)
			{
				for(int i = 0; i<m_FrameServer.RecentlyCapturedVideos.Count;i++)
				{
					if(m_FrameServer.RecentlyCapturedVideos[i].Filepath == box.FilePath)
					{
						m_FrameServer.RecentlyCapturedVideos.RemoveAt(i);
					}
				}
				
				DoUpdateCapturedVideos();
			}
		}
		
		#region Docking Undocking
		private void btnDockBottom_Click(object sender, EventArgs e)
		{
			DockKeyframePanel(!m_bDocked);
		}
		private void splitKeyframes_Panel2_DoubleClick(object sender, EventArgs e)
		{
			DockKeyframePanel(!m_bDocked);
		}
		private void DockKeyframePanel(bool _bDock)
		{
			if(_bDock)
			{
				// hide the keyframes, change image.
				splitKeyframes.SplitterDistance = splitKeyframes.Height - 25;				
				btnDockBottom.BackgroundImage = Resources.undock16x16;
				btnDockBottom.Visible = m_FrameServer.RecentlyCapturedVideos.Count > 0;
			}
			else
			{
				// show the keyframes, change image.
				splitKeyframes.SplitterDistance = splitKeyframes.Height - 140;
				btnDockBottom.BackgroundImage = Resources.dock16x16;
				btnDockBottom.Visible = true;
			}
			
			m_bDocked = _bDock;
		}
		#endregion

		#endregion

		#region Drawings Toolbar Events
		private void drawingTool_Click(object sender, EventArgs e)
		{
			// User clicked on a drawing tool button. A reference to the tool is stored in .Tag
			// Set this tool as the active tool (waiting for the actual use) and set the cursor accordingly.
			
			// Deactivate magnifier if not commited.
			if(m_FrameServer.Magnifier.Mode == MagnifierMode.Direct)
			{
				DisableMagnifier();
			}
			
			OnPoke();
			
			AbstractDrawingTool tool = ((ToolStripItem)sender).Tag as AbstractDrawingTool;
			if(tool != null)
			{
				m_ActiveTool = tool;
			}
			else
			{
				m_ActiveTool = m_PointerTool;
			}
			
			UpdateCursor();
			pbSurfaceScreen.Invalidate();
		}
		private void btnMagnifier_Click(object sender, EventArgs e)
		{
			if (m_FrameServer.IsConnected)
			{
				m_ActiveTool = m_PointerTool;

				if (m_FrameServer.Magnifier.Mode == MagnifierMode.None)
				{
					UnzoomDirectZoom();
					m_FrameServer.Magnifier.Mode = MagnifierMode.Direct;
					SetCursor(Cursors.Cross);
				}
				else if (m_FrameServer.Magnifier.Mode == MagnifierMode.Direct)
				{
					// Revert to no magnification.
					UnzoomDirectZoom();
					m_FrameServer.Magnifier.Mode = MagnifierMode.None;
					//btnMagnifier.BackgroundImage = Resources.magnifier2;
					SetCursor(m_PointerTool.GetCursor(0));
					pbSurfaceScreen.Invalidate();
				}
				else
				{
					DisableMagnifier();
					pbSurfaceScreen.Invalidate();
				}
			}
		}
		private void btnColorProfile_Click(object sender, EventArgs e)
		{
			OnPoke();

			// Load, save or modify current profile.
			FormToolPresets ftp = new FormToolPresets();
			FormsHelper.Locate(ftp);
			ftp.ShowDialog();
			ftp.Dispose();

			UpdateCursor();
			DoInvalidate();
		}
		private void UpdateCursor()
		{
			if(m_ActiveTool == m_PointerTool)
			{
				SetCursor(m_PointerTool.GetCursor(0));
			}
			else
			{
				SetCursor(m_ActiveTool.GetCursor(m_FrameServer.CoordinateSystem.Stretch));
			}
		}
		private void SetCursor(Cursor _cur)
		{
			pbSurfaceScreen.Cursor = _cur;
		}
		#endregion

		#region Context Menus Events
		
		#region Drawings Menus
		private void mnuConfigureDrawing_Click(object sender, EventArgs e)
		{
			if(m_FrameServer.Metadata.SelectedDrawing >= 0)
			{
				IDecorable decorableDrawing = m_FrameServer.Metadata[0].Drawings[m_FrameServer.Metadata.SelectedDrawing] as IDecorable;
				if(decorableDrawing != null &&  decorableDrawing.DrawingStyle != null && decorableDrawing.DrawingStyle.Elements.Count > 0)
				{
					FormConfigureDrawing2 fcd = new FormConfigureDrawing2(decorableDrawing.DrawingStyle, DoInvalidate);
					FormsHelper.Locate(fcd);
					fcd.ShowDialog();
					fcd.Dispose();
					DoInvalidate();
					this.ContextMenuStrip = popMenu;
				}
			}
		}
		private void mnuConfigureOpacity_Click(object sender, EventArgs e)
		{
			if(m_FrameServer.Metadata.SelectedDrawing >= 0)
			{
				formConfigureOpacity fco = new formConfigureOpacity(m_FrameServer.Metadata[0].Drawings[m_FrameServer.Metadata.SelectedDrawing], pbSurfaceScreen);
				FormsHelper.Locate(fco);
				fco.ShowDialog();
				fco.Dispose();
				pbSurfaceScreen.Invalidate();
			}
		}
		private void mnuDeleteDrawing_Click(object sender, EventArgs e)
		{
			DeleteSelectedDrawing();
			this.ContextMenuStrip = popMenu;
		}
		private void DeleteSelectedDrawing()
		{
			if (m_FrameServer.Metadata.SelectedDrawing >= 0)
			{
				IUndoableCommand cdd = new CommandDeleteDrawing(DoInvalidate, m_FrameServer.Metadata, m_FrameServer.Metadata[0].Position, m_FrameServer.Metadata.SelectedDrawing);
				CommandManager cm = CommandManager.Instance();
				cm.LaunchUndoableCommand(cdd);
				pbSurfaceScreen.Invalidate();
				this.ContextMenuStrip = popMenu;
			}
		}
		#endregion
		
		#region Magnifier Menus
		private void mnuMagnifierQuit_Click(object sender, EventArgs e)
		{
			DisableMagnifier();
			pbSurfaceScreen.Invalidate();
		}
		private void mnuMagnifierDirect_Click(object sender, EventArgs e)
		{
			// Use position and magnification to Direct Zoom.
			// Go to direct zoom, at magnifier zoom factor, centered on same point as magnifier.
			m_FrameServer.CoordinateSystem.Zoom = m_FrameServer.Magnifier.MagnificationFactor;
			m_FrameServer.CoordinateSystem.RelocateZoomWindow(m_FrameServer.Magnifier.Center);
			DisableMagnifier();
			m_FrameServer.Metadata.ResizeFinished();
			pbSurfaceScreen.Invalidate();
		}
		private void mnuMagnifierChangeMagnification(object sender, EventArgs e)
		{
		    ToolStripMenuItem menu = sender as ToolStripMenuItem;
		    if(menu == null)
		        return;
		    
		    foreach(ToolStripMenuItem m in maginificationMenus)
		        m.Checked = false;
		    
			menu.Checked = true;
			
			m_FrameServer.Magnifier.MagnificationFactor = (double)menu.Tag;
			DoInvalidate();
		}
		private void DisableMagnifier()
		{
			// Revert to no magnification.
			m_FrameServer.Magnifier.Mode = MagnifierMode.None;
			SetCursor(m_PointerTool.GetCursor(0));
		}
		#endregion

		#endregion
		
		#region DirectZoom
		private void UnzoomDirectZoom()
		{
			m_FrameServer.CoordinateSystem.ReinitZoom();
			m_PointerTool.SetZoomLocation(m_FrameServer.CoordinateSystem.Location);
			m_FrameServer.Metadata.ResizeFinished();
		}
		private void IncreaseDirectZoom()
		{
			if (m_FrameServer.Magnifier.Mode != MagnifierMode.None)
			{
				DisableMagnifier();
			}

			// Max zoom : 600%
			if (m_FrameServer.CoordinateSystem.Zoom < 6.0f)
			{
				m_FrameServer.CoordinateSystem.Zoom += 0.20f;
				RelocateDirectZoom();
				m_FrameServer.Metadata.ResizeFinished();
				ToastZoom();
			}
			
			pbSurfaceScreen.Invalidate();
		}
		private void DecreaseDirectZoom()
		{
			if (m_FrameServer.CoordinateSystem.Zoom > 1.2f)
			{
				m_FrameServer.CoordinateSystem.Zoom -= 0.20f;
			}
			else
			{
				m_FrameServer.CoordinateSystem.Zoom = 1.0f;	
			}
			
			RelocateDirectZoom();
			m_FrameServer.Metadata.ResizeFinished();
			ToastZoom();
			pbSurfaceScreen.Invalidate();
		}
		private void RelocateDirectZoom()
		{
			m_FrameServer.CoordinateSystem.RelocateZoomWindow();
			m_PointerTool.SetZoomLocation(m_FrameServer.CoordinateSystem.Location);
		}
		#endregion
		
		#region Toasts
		private void ToastZoom()
		{
			m_MessageToaster.SetDuration(750);
			int percentage = (int)(m_FrameServer.CoordinateSystem.Zoom * 100);
			m_MessageToaster.Show(String.Format(ScreenManagerLang.Toast_Zoom, percentage.ToString()));
		}
		private void ToastPause()
		{
			m_MessageToaster.SetDuration(750);
			m_MessageToaster.Show(ScreenManagerLang.Toast_Pause);
		}
		private void ToastDisconnect()
		{
			m_MessageToaster.SetDuration(1500);
			m_MessageToaster.Show(ScreenManagerLang.Toast_Disconnected);
		}
		private void ToastStartRecord()
		{
			m_MessageToaster.SetDuration(1000);
			m_MessageToaster.Show(ScreenManagerLang.Toast_StartRecord);
		}
		private void ToastStopRecord()
		{
			m_MessageToaster.SetDuration(750);
			m_MessageToaster.Show(ScreenManagerLang.Toast_StopRecord);
		}
		private void ToastImageSaved()
		{
			m_MessageToaster.SetDuration(750);
			m_MessageToaster.Show(ScreenManagerLang.Toast_ImageSaved);
		}
		#endregion
		
		#region Export video and frames
        private void tbImageFilename_TextChanged(object sender, EventArgs e)
        {
			if(!m_FilenameHelper.ValidateFilename(tbImageFilename.Text, true))
        	{
        		ScreenManagerKernel.AlertInvalidFileName();
        	}
        }
        private void tbVideoFilename_TextChanged(object sender, EventArgs e)
        {
        	if(!m_FilenameHelper.ValidateFilename(tbVideoFilename.Text, true))
        	{
        		ScreenManagerKernel.AlertInvalidFileName();
        	}
        }
		private void btnSnapShot_Click(object sender, EventArgs e)
		{
			// Export the current frame.
			if(!m_FrameServer.IsConnected)
			    return;
			
			if(!m_FilenameHelper.ValidateFilename(tbImageFilename.Text, false))
			{
			    ScreenManagerKernel.AlertInvalidFileName();
                return;
			}

			if(!Directory.Exists(PreferencesManager.CapturePreferences.ImageDirectory))
			    Directory.CreateDirectory(PreferencesManager.CapturePreferences.ImageDirectory);
			
            // In the meantime the other screen could have make a snapshot too,
            // which would have updated the last saved file name in the global prefs.
            // However we keep using the name of the last file saved in this specific screen to keep them independant.
            // for ex. the user might be saving to "Front - 4" on the left, and to "Side - 7" on the right.
            // This doesn't apply if we are using a pattern though.
            string filename = PreferencesManager.CapturePreferences.CaptureUsePattern ? m_FilenameHelper.InitImage() : tbImageFilename.Text;
            string filepath = PreferencesManager.CapturePreferences.ImageDirectory + "\\" + filename + m_FilenameHelper.GetImageFileExtension();
            
            if(!OverwriteOrCreateFile(filepath))
                return;
            
            Bitmap outputImage = m_FrameServer.GetFlushedImage();
            	
        	ImageHelper.Save(filepath, outputImage);
        	outputImage.Dispose();
        	
        	if(PreferencesManager.CapturePreferences.CaptureUsePattern)
        	{
        		m_FilenameHelper.AutoIncrement(true);
        		m_ScreenUIHandler.CaptureScreenUI_FileSaved();
        	}
        	
        	// Keep track of the last successful save.
        	// Each screen must keep its own independant history.
        	m_LastSavedImage = filename;
        	PreferencesManager.CapturePreferences.ImageFile = filename;
        	PreferencesManager.Save();
        	
        	// Update the filename for the next snapshot.
        	tbImageFilename.Text = PreferencesManager.CapturePreferences.CaptureUsePattern ? m_FilenameHelper.InitImage() : m_FilenameHelper.Next(m_LastSavedImage);
        	
        	ToastImageSaved();
		}
		private void btnRecord_Click(object sender, EventArgs e)
        {
            if(!m_FrameServer.IsConnected)
                return;
            
            if(m_FrameServer.IsRecording)
            {
                m_FrameServer.StopRecording();
                btnCamSettings.Enabled = true;
                EnableVideoFileEdit(true);
            
                // Keep track of the last successful save.
                PreferencesManager.CapturePreferences.VideoFile = m_LastSavedVideo;
                PreferencesManager.Save();
            
                // update file name.
                tbVideoFilename.Text = PreferencesManager.CapturePreferences.CaptureUsePattern ? m_FilenameHelper.InitVideo() : m_FilenameHelper.Next(m_LastSavedVideo);
            
                DisplayAsRecording(false);
            }
            else
            {
                // Start exporting frames to a video.
            
                if(!m_FilenameHelper.ValidateFilename(tbVideoFilename.Text, false))
                {
                    ScreenManagerKernel.AlertInvalidFileName();
                    return;
                }

                
                
                if(!Directory.Exists(PreferencesManager.CapturePreferences.VideoDirectory))
                    Directory.CreateDirectory(PreferencesManager.CapturePreferences.VideoDirectory);
                
                string filename = PreferencesManager.CapturePreferences.CaptureUsePattern ? m_FilenameHelper.InitVideo() : tbVideoFilename.Text;
                string filepath = PreferencesManager.CapturePreferences.VideoDirectory + "\\" + filename + m_FilenameHelper.GetVideoFileExtension();
             
                // Create embedded directory if needed
                string directory = Path.GetDirectoryName(filepath);
                if(!Directory.Exists(directory))
	               Directory.CreateDirectory(directory);
                
                // Check if file already exists.
                if(!OverwriteOrCreateFile(filepath))
                    return;
                
                if(PreferencesManager.CapturePreferences.CaptureUsePattern)
                {
                    m_FilenameHelper.AutoIncrement(false);
                    m_ScreenUIHandler.CaptureScreenUI_FileSaved();
                }
                    
                btnCamSettings.Enabled = false;
                m_LastSavedVideo = filename;
                m_FrameServer.CurrentCaptureFilePath = filepath;
                bool bRecordingStarted = m_FrameServer.StartRecording(filepath);
                if(bRecordingStarted)
                {
                    // Record will force grabbing if needed.
                    btnGrab.Image = Kinovea.ScreenManager.Properties.Resources.capturepause5;
                    EnableVideoFileEdit(false);
                    DisplayAsRecording(true);
                }
            }
            
            OnPoke();
        }
        private bool OverwriteOrCreateFile(string _filepath)
        {
        	// Check if the specified video file exists, and asks the user if he wants to overwrite.
        	bool bOverwriteOrCreate = true;
        	if(File.Exists(_filepath))
        	{
        		string msgTitle = ScreenManagerLang.Error_Capture_FileExists_Title;
        		string msgText = String.Format(ScreenManagerLang.Error_Capture_FileExists_Text, _filepath).Replace("\\n", "\n");
        		
        		DialogResult dr = MessageBox.Show(msgText, msgTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
        		if(dr != DialogResult.Yes)
        		{
        			bOverwriteOrCreate = false;
        		}
        	}
        	
        	return bOverwriteOrCreate;
        }
        private void EnableVideoFileEdit(bool _bEnable)
        {
        	lblVideoFile.Enabled = _bEnable;
        	tbVideoFilename.Enabled = _bEnable && !PreferencesManager.CapturePreferences.CaptureUsePattern;
			btnSaveVideoLocation.Enabled = _bEnable;        	
        }
        private void TextBoxes_MouseDoubleClick(object sender, MouseEventArgs e)
        {
        	TextBox tb = sender as TextBox;
        	if(tb != null)
        	{
        		tb.SelectAll();	
        	}
        }
        #endregion
        
		#region Device management
		private void btnCamSettings_Click(object sender, EventArgs e)
        {
			if(!m_FrameServer.IsRecording)
			{
				m_FrameServer.PromptDeviceSelector();
			}
        }
        private void tmrCaptureDeviceDetector_Tick(object sender, EventArgs e)
        {
        	if(!m_FrameServer.IsConnected)
        	{
        		TryToConnect();
        	}
        	else
        	{
        		CheckDeviceConnection();
        	}
        }
        private void TryToConnect()
        {
        	// Try to connect to a device.
    		// Prevent reentry.
    		if(!m_bTryingToConnect)
    		{
    			m_bTryingToConnect = true;        			
    			m_FrameServer.NegociateDevice();       			
    			m_bTryingToConnect = false;
    		}
        }
        private void CheckDeviceConnection()
        {
        	// Ensure we stay connected.
        	if(!m_bTryingToConnect)
    		{
    			m_bTryingToConnect = true;
    			m_FrameServer.HeartBeat();
    			m_bTryingToConnect = false;
    		}
        }
        private void BtnSaveImageLocationClick(object sender, EventArgs e)
        {
            OpenInExplorer(PreferencesManager.CapturePreferences.ImageDirectory);
        }
        private void BtnSaveVideoLocationClick(object sender, EventArgs e)
        {
            OpenInExplorer(PreferencesManager.CapturePreferences.VideoDirectory);
        }
        private void OpenInExplorer(string path)
        {
            if (!Directory.Exists(path))
                return;

            string arg = "\"" + path +"\"";
            System.Diagnostics.Process.Start("explorer.exe", arg);
        }
        #endregion
	}
}
