﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

using ASCOM.DeviceInterface;
using RestSharp;

namespace ASCOM.Remote
{
    /// <summary>
    /// ASCOM Remote Dome base class.
    /// </summary>
    public class DomeBaseClass : ReferenceCountedObjectBase, IDomeV2
    {
        #region Variables and Constants

        // Constant to set the device type
        private const string DEVICE_TYPE = "Dome";

        // Instance specific variables
        private TraceLoggerPlus TL; // Private variable to hold the trace logger object
        private string DriverNumber; // This driver's number in the series 1, 2, 3...
        private string DriverDisplayName; // Driver description that displays in the ASCOM Chooser.
        private string DriverProgId; // Drivers ProgID
        private SetupDialogForm setupForm; // Private variable to hold an instance of the Driver's setup form when invoked by the user
        private RestClient client; // Client to send and receive REST style messages to / from the remote server
        private uint clientNumber; // Unique number for this driver within the locaL server, i.e. across all drivers that the local server is serving
        private bool clientIsConnected;  // Connection state of this driver
        private string URIBase; // URI base unique to this driver

        // Variables to hold values that can be configured by the user through the setup form
        private bool traceState = true;
        private bool debugTraceState = true;
        private string ipAddressString;
        private decimal portNumber;
        private decimal remoteDeviceNumber;
        private string serviceType;
        private int establishConnectionTimeout;
        private int standardServerResponseTimeout;
        private int longServerResponseTimeout;
        private string userName;
        private string password;
        private bool manageConnectLocally;
        private SharedConstants.ImageArrayTransferType imageArrayTransferType;
        private SharedConstants.ImageArrayCompression imageArrayCompression;

        #endregion

        #region Initialiser

        /// <summary>
        /// Initializes a new instance of the <see cref="DomeBaseClass"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public DomeBaseClass(string RequiredDriverNumber, string RequiredDriverDisplayName, string RequiredProgId)
        {
            try
            {
                // Initialise variables unique to this particular driver with values passed from the calling class
                DriverNumber = RequiredDriverNumber;
                DriverDisplayName = RequiredDriverDisplayName; // Driver description that displays in the ASCOM Chooser.
                DriverProgId = RequiredProgId;

                if (TL == null) TL = new TraceLoggerPlus("", string.Format(SharedConstants.TRACELOGGER_NAME_FORMAT_STRING, DriverNumber, DEVICE_TYPE));
                RemoteClientDriver.ReadProfile(clientNumber, TL, DEVICE_TYPE, DriverProgId,
                    ref traceState, ref debugTraceState, ref ipAddressString, ref portNumber, ref remoteDeviceNumber, ref serviceType, ref establishConnectionTimeout, ref standardServerResponseTimeout,
                    ref longServerResponseTimeout, ref userName, ref password, ref manageConnectLocally, ref imageArrayTransferType, ref imageArrayCompression);
                TL.LogMessage(clientNumber, DEVICE_TYPE, string.Format("Trace state: {0}, Debug Trace State: {1}, TraceLogger Debug State: {2}", traceState, debugTraceState, TL.DebugTraceState));
                Version version = Assembly.GetEntryAssembly().GetName().Version;
                TL.LogMessage(clientNumber, DEVICE_TYPE, "Starting initialisation, Version: " + version.ToString());

                clientNumber = RemoteClientDriver.GetUniqueClientNumber();
                TL.LogMessage(clientNumber, DEVICE_TYPE, "This instance's unique client number: " + clientNumber);

                RemoteClientDriver.ConnectToRemoteServer(ref client, ipAddressString, portNumber, serviceType, TL, clientNumber, DEVICE_TYPE, standardServerResponseTimeout, userName, password);

                URIBase = string.Format("{0}{1}/{2}/{3}/", SharedConstants.API_URL_BASE, SharedConstants.API_VERSION_V1, DEVICE_TYPE, remoteDeviceNumber.ToString());
                TL.LogMessage(clientNumber, DEVICE_TYPE, "This devices's base URI: " + URIBase);
                TL.LogMessage(clientNumber, DEVICE_TYPE, "Establish communications timeout: " + establishConnectionTimeout.ToString());
                TL.LogMessage(clientNumber, DEVICE_TYPE, "Standard server response timeout: " + standardServerResponseTimeout.ToString());
                TL.LogMessage(clientNumber, DEVICE_TYPE, "Long server response timeout: " + longServerResponseTimeout.ToString());
                TL.LogMessage(clientNumber, DEVICE_TYPE, "User name: " + userName);
                TL.LogMessage(clientNumber, DEVICE_TYPE, string.Format("Password is Null or Empty: {0}, Password is Null or White Space: {1}", string.IsNullOrEmpty(password), string.IsNullOrWhiteSpace(password)));
                TL.LogMessage(clientNumber, DEVICE_TYPE, string.Format("Password length: {0}", password.Length));

                TL.LogMessage(clientNumber, DEVICE_TYPE, "Completed initialisation");
            }
            catch (Exception ex)
            {
                TL.LogMessageCrLf(clientNumber, DEVICE_TYPE, ex.ToString());
            }
        }

        #endregion

        #region Common properties and methods.

        public string Action(string actionName, string actionParameters)
        {
            RemoteClientDriver.SetClientTimeout(client, longServerResponseTimeout);
            return RemoteClientDriver.Action(clientNumber, client, URIBase, TL, actionName, actionParameters);
        }

        public void CommandBlind(string command, bool raw = false)
        {
            RemoteClientDriver.SetClientTimeout(client, longServerResponseTimeout);
            RemoteClientDriver.CommandBlind(clientNumber, client, URIBase, TL, command, raw);
        }

        public bool CommandBool(string command, bool raw = false)
        {
            RemoteClientDriver.SetClientTimeout(client, longServerResponseTimeout);
            return RemoteClientDriver.CommandBool(clientNumber, client, URIBase, TL, command, raw);
        }

        public string CommandString(string command, bool raw = false)
        {
            RemoteClientDriver.SetClientTimeout(client, longServerResponseTimeout);
            return RemoteClientDriver.CommandString(clientNumber, client, URIBase, TL, command, raw);
        }

        public void Dispose()
        {
        }

        public bool Connected
        {
            get
            {
                return clientIsConnected;
            }
            set
            {
                clientIsConnected = value;
                if (manageConnectLocally)
                {
                    TL.LogMessage(clientNumber, DEVICE_TYPE, string.Format("The Connected property is being managed locally so the new value '{0}' will not be sent to the remote server", value));
                }
                else // Send the command to the remote server
                {
                    RemoteClientDriver.SetClientTimeout(client, establishConnectionTimeout);
                    if (value) RemoteClientDriver.Connect(clientNumber, client, URIBase, TL);
                    else RemoteClientDriver.Disconnect(clientNumber, client, URIBase, TL);
                }
            }
        }

        public string Description
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                string response = RemoteClientDriver.Description(clientNumber, client, URIBase, TL);
                TL.LogMessage(clientNumber, "Description", response);
                return response;
            }
        }

        public string DriverInfo
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                string response = $"ASCOM Remote Driver v{version} - REMOTE DEVICE: {RemoteClientDriver.DriverInfo(clientNumber, client, URIBase, TL)}";
                TL.LogMessage(clientNumber, "DriverInfo", response);
                return response;
            }
        }

        public string DriverVersion
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.DriverVersion(clientNumber, client, URIBase, TL);
            }
        }

        public short InterfaceVersion
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.InterfaceVersion(clientNumber, client, URIBase, TL);
            }
        }

        public string Name
        {
            get
            {
                string remoteString = RemoteClientDriver.GetValue<string>(clientNumber, client, URIBase, TL, "Name");
                string response = string.Format("{0} REMOTE DRIVER: {1}", DriverDisplayName, remoteString);
                TL.LogMessage(clientNumber, "Name", response);
                return response;
            }
        }

        public void SetupDialog()
        {
            TL.LogMessage(clientNumber, "SetupDialog", "Connected: " + clientIsConnected.ToString());
            if (clientIsConnected)
            {
                MessageBox.Show("Simulator is connected, setup parameters cannot be changed, please press OK");
            }
            else
            {
                TL.LogMessage(clientNumber, "SetupDialog", "Creating setup form");
                using (setupForm = new SetupDialogForm(TL))
                {
                    // Pass the setup dialogue data into the form
                    setupForm.DriverDisplayName = DriverDisplayName;
                    setupForm.TraceState = traceState;
                    setupForm.DebugTraceState = debugTraceState;
                    setupForm.ServiceType = serviceType;
                    setupForm.IPAddressString = ipAddressString;
                    setupForm.PortNumber = portNumber;
                    setupForm.RemoteDeviceNumber = remoteDeviceNumber;
                    setupForm.EstablishConnectionTimeout = establishConnectionTimeout;
                    setupForm.StandardTimeout = standardServerResponseTimeout;
                    setupForm.LongTimeout = longServerResponseTimeout;
                    setupForm.UserName = userName;
                    setupForm.Password = password;
                    setupForm.ManageConnectLocally = manageConnectLocally;
                    setupForm.ImageArrayTransferType = imageArrayTransferType;
                    setupForm.DeviceType = DEVICE_TYPE;

                    TL.LogMessage(clientNumber, "SetupDialog", "Showing Dialogue");
                    var result = setupForm.ShowDialog();
                    TL.LogMessage(clientNumber, "SetupDialog", "Dialogue closed");
                    if (result == DialogResult.OK)
                    {
                        TL.LogMessage(clientNumber, "SetupDialog", "Dialogue closed with OK status");

                        // Retrieve revised setup data from the form
                        traceState = setupForm.TraceState;
                        debugTraceState = setupForm.DebugTraceState;
                        serviceType = setupForm.ServiceType;
                        ipAddressString = setupForm.IPAddressString;
                        portNumber = setupForm.PortNumber;
                        remoteDeviceNumber = setupForm.RemoteDeviceNumber;
                        establishConnectionTimeout = (int)setupForm.EstablishConnectionTimeout;
                        standardServerResponseTimeout = (int)setupForm.StandardTimeout;
                        longServerResponseTimeout = (int)setupForm.LongTimeout;
                        userName = setupForm.UserName;
                        password = setupForm.Password;
                        manageConnectLocally = setupForm.ManageConnectLocally;
                        imageArrayTransferType = setupForm.ImageArrayTransferType;

                        // Write the changed values to the Profile
                        TL.LogMessage(clientNumber, "SetupDialog", "Writing new values to profile");
                        RemoteClientDriver.WriteProfile(clientNumber, TL, DEVICE_TYPE, DriverProgId,
                             traceState, debugTraceState, ipAddressString, portNumber, remoteDeviceNumber, serviceType, establishConnectionTimeout, standardServerResponseTimeout, longServerResponseTimeout, userName, password, manageConnectLocally, imageArrayTransferType, imageArrayCompression);

                        // Establish new host and device parameters
                        TL.LogMessage(clientNumber, "SetupDialog", "Establishing new host and device parameters");
                        RemoteClientDriver.ConnectToRemoteServer(ref client, ipAddressString, portNumber, serviceType, TL, clientNumber, DEVICE_TYPE, standardServerResponseTimeout, userName, password);
                    }
                    else TL.LogMessage(clientNumber, "SetupDialog", "Dialogue closed with Cancel status");
                }
                if (!(setupForm == null))
                {
                    setupForm.Dispose();
                    setupForm = null;
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.SupportedActions(clientNumber, client, URIBase, TL);
            }
        }

        #endregion

        #region IDomeV2 Implementation

        public void AbortSlew()
        {
            RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
            RemoteClientDriver.CallMethodWithNoParameters(clientNumber, client, URIBase, TL, "AbortSlew");
        }

        public void CloseShutter()
        {
            RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
            RemoteClientDriver.CallMethodWithNoParameters(clientNumber, client, URIBase, TL, "CloseShutter");
        }

        public void FindHome()
        {
            RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
            RemoteClientDriver.CallMethodWithNoParameters(clientNumber, client, URIBase, TL, "FindHome");
        }

        public void OpenShutter()
        {
            RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
            RemoteClientDriver.CallMethodWithNoParameters(clientNumber, client, URIBase, TL, "OpenShutter");
        }

        public void Park()
        {
            RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
            RemoteClientDriver.CallMethodWithNoParameters(clientNumber, client, URIBase, TL, "Park");
        }

        public void SetPark()
        {
            RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
            RemoteClientDriver.CallMethodWithNoParameters(clientNumber, client, URIBase, TL, "SetPark");
        }

        public void SlewToAltitude(double Altitude)
        {
            Dictionary<string, string> Parameters = new Dictionary<string, string>
            {
                { SharedConstants.ALT_PARAMETER_NAME, Altitude.ToString(CultureInfo.InvariantCulture) }
            };
            RemoteClientDriver.SendToRemoteDriver<NoReturnValue>(clientNumber, client, URIBase, TL, "SlewToAltitude", Parameters, Method.PUT);
        }

        public void SlewToAzimuth(double Azimuth)
        {
            Dictionary<string, string> Parameters = new Dictionary<string, string>
            {
                { SharedConstants.AZ_PARAMETER_NAME, Azimuth.ToString(CultureInfo.InvariantCulture) }
            };
            RemoteClientDriver.SendToRemoteDriver<NoReturnValue>(clientNumber, client, URIBase, TL, "SlewToAzimuth", Parameters, Method.PUT);
        }

        public void SyncToAzimuth(double Azimuth)
        {
            Dictionary<string, string> Parameters = new Dictionary<string, string>
            {
                { SharedConstants.AZ_PARAMETER_NAME, Azimuth.ToString(CultureInfo.InvariantCulture) }
            };
            RemoteClientDriver.SendToRemoteDriver<NoReturnValue>(clientNumber, client, URIBase, TL, "SyncToAzimuth", Parameters, Method.PUT);
        }

        public double Altitude
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<double>(clientNumber, client, URIBase, TL, "Altitude");
            }
        }

        public bool AtHome
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "AtHome");
            }
        }

        public bool AtPark
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "AtPark");
            }
        }

        public double Azimuth
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<double>(clientNumber, client, URIBase, TL, "Azimuth");
            }
        }

        public bool CanFindHome
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanFindHome");
            }
        }

        public bool CanPark
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanPark");
            }
        }

        public bool CanSetAltitude
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanSetAltitude");
            }
        }

        public bool CanSetAzimuth
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanSetAzimuth");
            }
        }

        public bool CanSetPark
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanSetPark");
            }
        }

        public bool CanSetShutter
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanSetShutter");
            }
        }

        public bool CanSlave
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanSlave");
            }
        }

        public bool CanSyncAzimuth
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "CanSyncAzimuth");
            }
        }

        public ShutterState ShutterStatus
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<ShutterState>(clientNumber, client, URIBase, TL, "ShutterStatus");
            }
        }

        public bool Slaved
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "Slaved");
            }

            set
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                RemoteClientDriver.SetBool(clientNumber, client, URIBase, TL, "Slaved", value);
            }
        }

        public bool Slewing
        {
            get
            {
                RemoteClientDriver.SetClientTimeout(client, standardServerResponseTimeout);
                return RemoteClientDriver.GetValue<bool>(clientNumber, client, URIBase, TL, "Slewing");
            }
        }

        #endregion

    }
}
