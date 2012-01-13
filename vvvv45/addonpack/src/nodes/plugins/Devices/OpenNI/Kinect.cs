﻿#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Core.Logging;

using OpenNI;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "Kinect",
	            Category = "Devices",
	            Version = "OpenNI",
	            Help = "Provides access to a Kinect through the OpenNI API",
	            Author = "Phlegma")]
	#endregion PluginInfo
	public class KinectContext: IPluginEvaluate, IDisposable
	{
		#region fields & pins
		//vvvv
		[Input("Mirrored", IsSingle = true, DefaultValue = 0)]
		IDiffSpread<bool> FMirrored;

		[Input("Enabled", IsSingle = true, DefaultValue = 1)]
		IDiffSpread<bool> FUpdateIn;

		[Output("Context")]
		ISpread<Context> FContextOut;

		[Import()]
		ILogger FLogger;

		//Kinect
		private bool FRunning;
		private Context FContext;
		private ImageGenerator FImageGenerator;
		private DepthGenerator FDepthGenerator;
		private Thread FUpdater;
		#endregion fields & pins
		
		public KinectContext()
		{
			OpenContext();
			
			FUpdater = new Thread(Update);
			FRunning = true;
			FUpdater.Start();
		}

		#region Evaluate

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if (FContext != null)
			{
				if (FMirrored.IsChanged)
					FContext.GlobalMirror = FMirrored[0];
			}

			//writes the Context Object to the Output for
			//is required for other generators
			FContextOut[0] = FContext;
		}
		#endregion
		
		#region Open and close Context
		private void OpenContext()
		{
			//try to open Kinect Context
			try
			{
				FContext = new Context();
				FContext.ErrorStateChanged += FContext_ErrorStateChanged;
				
				FImageGenerator = (ImageGenerator) FContext.CreateAnyProductionTree(OpenNI.NodeType.Image, null);
				FDepthGenerator = (DepthGenerator) FContext.CreateAnyProductionTree(OpenNI.NodeType.Depth, null);
				FDepthGenerator.AlternativeViewpointCapability.SetViewpoint(FImageGenerator);
				
				FContext.StartGeneratingAll();
				
				var version = OpenNI.Version.Current.Major.ToString() + "." + OpenNI.Version.Current.Minor.ToString() + "." + OpenNI.Version.Current.Maintenance.ToString() + "." + OpenNI.Version.Current.Build.ToString();
				//FLogger.Log(LogType.Message, "OpenNI Version: " + version);
			}
			catch (StatusException ex)
			{
				FLogger.Log(ex);
			}
			catch (GeneralException e)
			{
				FLogger.Log(e);
			}
		}
		
		private void CloseContext()
		{
			if (FUpdater.IsAlive)
			{
				//wait for threadloop to exit
				FRunning = false;
				FUpdater.Join();
			}

			if (FContext != null)
			{
				FContext.StopGeneratingAll();
				FContext.ErrorStateChanged -= FContext_ErrorStateChanged;
				FImageGenerator.Dispose();
				//FDepthGenerator.Dispose();
				
				FContext.Shutdown();
				FContext = null;
			}
		}
		#endregion

		#region Error Event
		void FContext_ErrorStateChanged(object sender, ErrorStateEventArgs e)
		{
			FLogger.Log(LogType.Error, "Global Kinect Error: " + e.CurrentError);
		}
		#endregion

		#region Update Thread
		private void Update()
		{
			while (FRunning)
			{
				try
				{
					//The way how to update
					if (FContext != null)
						FContext.WaitAnyUpdateAll();
				}
				catch (StatusException ey)
				{
					Debug.WriteLine(ey.Message);
				}
				catch (GeneralException ez)
				{
					Debug.WriteLine(ez.Message);
				}
				catch (AccessViolationException ex)
				{
					Debug.WriteLine(ex.Message);
				}
			}
		}
		#endregion

		#region Dispose
		public void Dispose()
		{
			CloseContext();
		}
		#endregion Dispose
	}
}