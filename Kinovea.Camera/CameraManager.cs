﻿#region License
/*
Copyright © Joan Charmant 2012.
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
using System;
using System.Collections.Generic;
using Kinovea.Services;

namespace Kinovea.Camera
{
    /// <summary>
    /// A camera manager is a bridge with a technology (not with a single camera).
    /// For example, we have a camera manager for DirectShow, another for HTTP connected cameras.
    /// It is responsible for discovering cameras of its type and instanciating a corresponding frame grabber.
    /// </summary>
    public abstract class CameraManager
    {
        /// <summary>
        /// Event raised by Camera managers to report a new image. (events can't be inherited).
        /// </summary>
        public event EventHandler<CameraImageReceivedEventArgs> CameraImageReceived;
        protected virtual void OnCameraImageReceived(CameraImageReceivedEventArgs e)
        {
            EventHandler<CameraImageReceivedEventArgs> invoker = CameraImageReceived;
            if(invoker != null) 
                invoker(this, e);
        }
        
        public abstract string CameraType { get; }
        
        
        /// <summary>
        /// Get the list of reachable cameras, try to connect to each of them to get a snapshot, and return a small summary of the device.
        /// Knowing about the camera is enough, the camera managers should cache the snapshots to avoid connecting to the camera each time.
        /// </summary>
        public abstract List<CameraSummary> DiscoverCameras(IEnumerable<CameraBlurb> blurbs);
        
        /// <summary>
        /// Get a single image for thumbnail refresh.
        /// The function is asynchronous and should raise CameraImageReceived when done.
        /// </summary>
        public abstract void GetSingleImage(CameraSummary summary);
        
        /// <summary>
        /// Extract a camera blurb (used for XML persistence) from a camera summary.
        /// </summary>
        public abstract CameraBlurb BlurbFromSummary(CameraSummary summary);
        
        /// <summary>
        /// Connect to a camera and return the frame grabbing object.
        /// </summary>
        public abstract IFrameGrabber Connect(CameraSummary summary);
        
        /// <summary>
        /// Launch a dialog to configure the device. Returns true if the configuration has changed.
        /// </summary>
        public abstract bool Configure(CameraSummary summary);
        
        /// <summary>
        /// Returns a small non-translatable text to be displayed in the header line above the image.
        /// </summary>
        public abstract string GetSummaryAsText(CameraSummary summary);
        
        /// <summary>
        /// Called after the user personalize a camera. Should persist the customized information.
        /// </summary>
        public void UpdatedCameraSummary(CameraSummary summary)
        {
            PreferencesManager.CapturePreferences.AddCamera(BlurbFromSummary(summary));
            PreferencesManager.Save();
        }
    }
}