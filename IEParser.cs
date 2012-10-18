////////////////////////////////////////////////////////////////

#region Header

//
// Copyright (c) 2007-2010 MetaGeek, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#endregion Header

////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ManagedWifi
{
    public static class IeParser
    {

        public struct InformationElement
        {
            public ushort ItsNumber { get; set; } 
            public ushort ItsLength { get; set; } 
            public byte[] ItsData { get; set; } 
        }

        #region Public Methods

        public static TypeNSettings Parse(byte[] ies)
        {

            var informationElements = BuildInformationElements(ies);
            var settings = new TypeNSettings();
            bool returnNull = true;

            foreach (var informationElement in informationElements)
            {
                switch (informationElement.ItsNumber)
                {
                    case 45: //HT Capabilities
                        ParseHTCapabilities(informationElement, settings);
                        returnNull = false;
                        break;
                    case 61: //HT Information
                        ParseHTOperation(informationElement, settings);
                        returnNull = false;
                        break;
                }
            }

            return returnNull ? null : settings;
        }

        public static TypeACSettings ParseAC(byte[] ies)
        {

            var informationElements = BuildInformationElements(ies);
            var settings = new TypeACSettings();
            bool returnNull = true;

            foreach (var informationElement in informationElements)
            {
                switch (informationElement.ItsNumber)
                {
                    case 45: //HT Capabilities
                        ParseHTCapabilities(informationElement, settings);
                        returnNull = false;
                        break;
                    case 61: //HT Information
                        ParseHTOperation(informationElement, settings);
                        returnNull = false;
                        break;
                    case 191: //VHT Capabilities
                        ParseVHTCapabilities(informationElement, settings);
                        break;
                    case 192: //VHT Operation
                        ParseVHTOperation(informationElement, settings);
                        break;
                }
            }

            return returnNull ? null : settings;
        }



        private static void ParseVHTOperation(InformationElement ie, TypeACSettings settings)
        {
            settings.Operations = new TypeACSettings.VHTOperations();

            var operations = new byte[4];
            Array.Copy(ie.ItsData, 0, operations, 0, 3);

            var basicMCSSet = new byte[8];
            Array.Copy(ie.ItsData, 3, basicMCSSet, 0, 2);

            settings.Operations.ChannelWidth = 
                (TypeACSettings.VHTOperations.VHTChannelWidth) Enum.Parse(
                    typeof(TypeACSettings.VHTOperations.VHTChannelWidth), 
                    operations[0].ToString(CultureInfo.InvariantCulture)
                    );

        }

        private static void ParseVHTCapabilities(InformationElement ie, TypeACSettings settings)
        {
            settings.Capabilities = new TypeACSettings.VHTCapabilities();

            var capabilities = new byte[4];
            Array.Copy(ie.ItsData, 0, capabilities, 0, 4);

            var supportedMCS = new byte[8];
            Array.Copy(ie.ItsData, 4, supportedMCS, 0, 8);

            settings.Capabilities.ShortGi160MHz = (capabilities[0] & 0x40) == 0x40;
            settings.Capabilities.ShortGi80MHz = (capabilities[0] & 0x20) == 0x20;

            settings.Capabilities.MaxRecieveRate = BitConverter.ToUInt16(supportedMCS, 2);
            settings.Capabilities.MaxTransmitRate = BitConverter.ToUInt16(supportedMCS, 6);

            var supportedChannelWidth = ( capabilities[0] & 0x0C ) >> 2;

            settings.Capabilities.SupportedWidth =
                (TypeACSettings.VHTCapabilities.VHTSupportedWidth)
                Enum.Parse(typeof (TypeACSettings.VHTCapabilities.VHTSupportedWidth),
                        supportedChannelWidth.ToString(CultureInfo.InvariantCulture)
                    );
        }

        private static void ParseHTOperation(InformationElement ie, TypeNSettings settings)
        {
            const int channel = 0;
            const int subset1 = 1;

            //Primary channel
            settings.PrimaryChannel = ie.ItsData[0]; 

            //Secondary channel location
            settings.SecondaryChannelLower = (ie.ItsData[channel] & 0x03) == 0x03;

            //Check if there is no secondary channel and set 40MHz to false
            if (settings.Is40Mhz)
                settings.Is40Mhz = (ie.ItsData[subset1] & 0x03) == 0x03 || (ie.ItsData[subset1] & 0x01) == 0x01;
        }

        private static void ParseHTCapabilities(InformationElement ie, TypeNSettings settings)
        {
            settings.Is40Mhz = ((ie.ItsData[0] & 0x02) == 0x02);

            settings.ShortGi20MHz = (ie.ItsData[0] & 0x20) == 0x20;
            settings.ShortGi40MHz = (ie.ItsData[0] & 0x40) == 0x40;

            //Get supported MCS indexes 
            //1 bit per index

            byte[] bits = new byte[4];
            //Array.ConstrainedCopy(ies, index + 5, bits, 0, 4);
            Array.Copy(ie.ItsData, 4, bits, 0, 4);

            BitArray b = new BitArray(bits);
            //settings.Rates = new List<double>();

            //The MCS indexes are in little endian,
            //so this loop will start at the lowest rates
            for (int i = 0; i < b.Length; i++)
            {
                   //If the MCS index bit is 0, skip it
                if (b[i] == false) continue;

                //Add the rate
                settings.Rates.Add(McsSet.GetSpeed((uint) i, settings.ShortGi20MHz, settings.ShortGi40MHz,
                                                   settings.Is40Mhz));
            }
        }

        private static IEnumerable<InformationElement> BuildInformationElements(byte[] ies)
        {
            var informationElements = new List<InformationElement>();

            var index = 0;

            while (index < ies.Length)
            {
                var ie = new InformationElement();
                ie.ItsNumber = ies[index];
                ie.ItsLength = ies[index + 1];
                ie.ItsData = new byte[ie.ItsLength];
                Array.Copy(ies, index + 2, ie.ItsData, 0, ie.ItsLength);

                informationElements.Add(ie);
                index += ie.ItsLength + 2;
            }
            return informationElements;
        }


        public class TypeNSettings
        {
            public bool Is40Mhz;
            public bool ShortGi20MHz;
            public bool ShortGi40MHz;
            public uint PrimaryChannel;
            public bool SecondaryChannelLower;

            //public uint MaxMcs;
            public List<double> Rates;

            //public static TypeNSettings Empty = new TypeNSettings() { Rates = new List<double>() };
            public TypeNSettings()
            {
                Rates = new List<double>();
            }

            public TypeNSettings(TypeNSettings settings)
            {
                Is40Mhz = settings.Is40Mhz;
                ShortGi20MHz = settings.ShortGi20MHz;
                ShortGi40MHz = settings.ShortGi40MHz;
                PrimaryChannel = settings.PrimaryChannel;
                SecondaryChannelLower = settings.SecondaryChannelLower;
                //MaxMcs = settings.MaxMcs;
                Rates = settings.Rates;
            }

            public override bool Equals(object obj)
            {
                if (obj is TypeNSettings)
                {
                    TypeNSettings set = (TypeNSettings)obj;
                    bool yes = set.Is40Mhz == Is40Mhz;
                    yes &= set.ShortGi20MHz == ShortGi20MHz;
                    yes &= set.ShortGi40MHz == ShortGi40MHz;
                    yes &= set.PrimaryChannel == PrimaryChannel;
                    yes &= set.SecondaryChannelLower == SecondaryChannelLower;
                    //Don't compare rates

                    return yes;
                }
                return false;
            }
        }

        public class TypeACSettings : TypeNSettings
        {
            public class VHTCapabilities
            {
                public enum VHTSupportedWidth
                {
                    Eighty = 0x00,
                    OneSixty = 0x01,
                    All  = 0x02
                }

                public bool ShortGi80MHz;
                public bool ShortGi160MHz;
                public bool Supports160Mhz;
                public bool Supports80Plus80Mhz;
                public ushort MaxRecieveRate;
                public ushort MaxTransmitRate;
                public VHTSupportedWidth SupportedWidth;
            }

            public class VHTOperations
            {
                public enum VHTChannelWidth : byte
                {
                    TwentyOrForty = 0x00,
                    Eighty = 0x01,
                    OneSixty = 0x02,
                    EightyPlusEighty = 0x03
                }

                public VHTChannelWidth ChannelWidth;
            }

            public VHTCapabilities Capabilities { get; set; }
            public VHTOperations Operations { get; set; }
        }


        #endregion Public Methods

        #region Private Methods

        private class McsSet
        {
            //20MHz long GI
            private static readonly Dictionary<uint, float> LGiTable20 = new Dictionary<uint, float>
                                                                              {
                                                                {0, 6f},//6.5
                                                                {1, 13f},
                                                                {2, 19f}, //19.5
                                                                {3, 26f},
                                                                {4, 39f},
                                                                {5, 52f},
                                                                {6, 58f},
                                                                {7, 65f}
                                                            };

            //20MHz short GI
            private static readonly Dictionary<uint, float> SGiTable20 = new Dictionary<uint, float>
                                                                              {
                                                                {0, 7f}, //7.2
                                                                {1, 14f},//14.4
                                                                {2, 22f},//21.7
                                                                {3, 29f},//28.9
                                                                {4, 43f},//43.3
                                                                {5, 58f},//57.8
                                                                {6, 65f},
                                                                {7, 72f} //72.2
                                                            };

            //40MHz long GI
            private static readonly Dictionary<uint, float> LGiTable40 = new Dictionary<uint, float>
                                                                              {
                                                                {0, 13f}, //13.5
                                                                {1, 27f},
                                                                {2, 40f},//40.5
                                                                {3, 54f},
                                                                {4, 81f},
                                                                {5, 108f},
                                                                {6, 121f},
                                                                {7, 135f}
                                                            };

            //40MHz short GI
            private static readonly Dictionary<uint, float> SGiTable40 = new Dictionary<uint, float>
                                                                              {
                                                                {0, 15f},
                                                                {1, 30f},
                                                                {2, 45f},
                                                                {3, 60f},
                                                                {4, 90f},
                                                                {5, 120f},
                                                                {6, 135f},
                                                                {7, 150f}
                                                            };

            public static float GetSpeed(uint index, bool shortGi20MHz, bool shortGi40MHz, bool fortyMHz)
            {
                float output;

                if (index > 32) return 0f;
                int streams = 0;

                if (index >= 0 && index < 8)
                {
                    streams = 1;
                }
                else if (index >= 8 && index < 16)
                {
                    streams = 2;
                    index -= 8;
                }
                else if (index >= 16 && index < 24)
                {
                    streams = 3;
                    index -= 16;
                }
                else if (index >= 24 && index < 32)
                {
                    streams = 4;
                    index -= 24;
                }

                if (fortyMHz)
                {
                    if (shortGi40MHz)
                    {
                        output = SGiTable40[index];
                        output *= streams;
                    }
                    else
                    {
                        output = LGiTable40[index];
                        output *= streams;
                    }
                }
                else //20 MHz channel
                {
                    if (shortGi20MHz)
                    {
                        output = SGiTable20[index];
                        output *= streams;
                    }
                    else
                    {
                        output = LGiTable20[index];
                        output *= streams;
                    }
                }

                return output;
            }
        }

        #endregion Private Methods
    }

    
}