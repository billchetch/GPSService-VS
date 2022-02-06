using System;
using System.Collections.Generic;
using System.Timers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Services;
using Chetch.GPS;
using System.Diagnostics;
using Chetch.Messaging;
using System.Globalization;

namespace GPSService
{
    public class GPSService : TCPMessagingClient
    {
        private GPSDB _gpsdb;
        private GPSManager _gpsMgr;
        private Timer _gpstimer;

        
        public GPSService(bool test = false) : base("GPSS", test ? null : "GPSSClient", "GPSService", test ? null : "GPSServiceLog")
        {
            try
            {
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Connecting to GPS database...");
                _gpsdb = GPSDB.Create(Properties.Settings.Default);
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Connected to GPS database");
            }
            catch (Exception e)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 0, e.Message);
                throw e;
            }
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            try
            {
                String device = Properties.Settings.Default.GPSDevice;
                _gpsMgr = new GPSManager(device, _gpsdb);
                _gpsMgr.Tracing = Tracing;
                _gpsMgr.LogPositionWait = 60 * 1000; //record position every minute
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Created GPS manager for device {0}", device);


                //create timer
                _gpstimer = new Timer();
                _gpstimer.Interval = 2000;
                _gpstimer.Elapsed += new ElapsedEventHandler(this.MonitorGPS);
                _gpstimer.Start();
                Tracing?.TraceEvent(TraceEventType.Information, 0, "Created GPS monitor timer at intervals of {0}", _gpstimer.Interval);
            }
            catch (Exception e)
            {
                Tracing?.TraceEvent(TraceEventType.Error, 0, e.Message);
                throw e;
            }
        }

        protected override void OnStop()
        {
            base.OnStop();

            _gpstimer?.Stop();
            _gpsMgr?.StopRecording();
        }

        public override void AddCommandHelp()
        {
            base.AddCommandHelp();

            AddCommandHelp("status", "Get status of service");
            AddCommandHelp("(p)osition", "Get position at for a certain <date?>. Leave blank for latest position. Date is in mysql format.");
        }

        public override void HandleClientError(Connection cnn, Exception e)
        {
            Tracing?.TraceEvent(TraceEventType.Error, 0, e.Message);
        }

        public override bool HandleCommand(Connection cnn, Message message, string command, List<object> args, Message response)
        {
            switch (command)
            {
                case "status":
                    if (_gpsMgr == null)
                    {
                        throw new Exception("No GPS Manager available");
                    }
                    response.AddValue("DeviceConnected", _gpsMgr.IsConnected);
                    response.AddValue("DeviceState", _gpsMgr.CurrentState.ToString());
                    if (_gpsMgr.CurrentState == GPSManager.State.RECORDING)
                    {
                        response.AddValue("CurrentPosition", _gpsMgr.CurrentPosition.ToString());
                    }
                    break;

                case "p":
                case "position":
                    if (_gpsMgr == null)
                    {
                        throw new Exception("GPS Manager has not been created");
                    }

                    GPSManager.GPSPositionData pos;
                    if (args.Count == 0)
                    {
                        if (_gpsMgr.CurrentState != GPSManager.State.RECORDING)
                        {
                            throw new Exception("GPS Manager is in state " + _gpsMgr.CurrentState + " so cannot provide current position data");
                        }
                        pos = _gpsMgr.CurrentPosition;
                    }
                    else
                    {
                        //get historical position
                        pos = new GPSManager.GPSPositionData();
                        String dts = args[0].ToString() + " " + (args.Count == 1 ? "00:00:00" : args[1]);
                        GPSDB.GPSPositionRow row = _gpsdb.GetNearestPosition(dts);
                        if (row != null)
                        {
                            row.Assign(pos);
                        }
                    }

                    response.AddValue("Latitude", pos.Latitude);
                    response.AddValue("Longitude", pos.Longitude);
                    response.AddValue("Bearing", pos.Bearing);
                    response.AddValue("Speed", pos.Speed);
                    response.AddValue("HDOP", pos.HDOP);
                    response.AddValue("VDOP", pos.VDOP);
                    response.AddValue("PDOP", pos.PDOP);
                    response.AddValue("Timestamp", pos.Timestamp);
                    DateTime dt = new DateTime(pos.Timestamp * TimeSpan.TicksPerMillisecond);
                    response.AddValue("DateTimeUTC", dt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    response.AddValue("NowUTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    break;
                    
            }

            return true;
        }

        public void MonitorGPS(Object sender, ElapsedEventArgs eventArgs)
        {
            if (!_gpsMgr.IsConnected)
            {
                _gpstimer.Stop();
                try
                {
                    Tracing?.TraceEvent(TraceEventType.Information, 0, "GPS not connected so connecting...");
                    _gpsMgr.StartRecording();
                    Tracing?.TraceEvent(TraceEventType.Information, 0, "GPS connected");
                    _gpstimer.Interval = 10000;
                }
                catch (Exception e)
                {
                    Tracing?.TraceEvent(TraceEventType.Error, 0, "MontiorGPS: {0}", e.Message);
                }
                finally
                {
                    _gpstimer.Start();
                }
            }
        } //end timer monitor GPS device connedted
    }
}
