/*
* Copyright (c) 2018, NVIDIA CORPORATION.  All rights reserved.
*
* NVIDIA CORPORATION and its licensors retain all intellectual property
* and proprietary rights in and to this software, related documentation
* and any modifications thereto.  Any use, reproduction, disclosure or
* distribution of this software and related documentation without an express
* license agreement from NVIDIA CORPORATION is strictly prohibited.
*/

using System.Collections.Generic;
using System.Runtime.InteropServices;
//using UnityEngine;
using System.Text;
using System;

namespace NVIDIA
{
    public class Highlights
    {

        public static bool instanceCreated = false;

        public enum HighlightScope
        {
            Highlights = 0x00,
            HighlightsRecordVideo = 0x01,
            HighlightsRecordScreenshot = 0x02,
            Ops = 0x03,
            MAX
        }

        public enum HighlightType
        {
            None = 0x00,
            Milestone = 0x01,
            Achievement = 0x02,
            Incident = 0x04,
            StateChange = 0x08,
            Unannounced = 0x10,
            MAX = 0x20
        };

        public enum HighlightSignificance
        {
            None = 0x00,
            ExtremelyBad = 0x01,
            VeryBad = 0x02,
            Bad = 0x04,
            Neutral = 0x10,
            Good = 0x100,
            VeryGood = 0x200,
            ExtremelyGood = 0x400,
            MAX = 0x800
        };

        public enum Permission
        {
            Granted = 0,
            Denied = 1,
            MustAsk = 2,
            Unknown = 3,
            MAX = 4,
        };

        public enum LogLevel
        {
            None = 0,
            Error = 1,
            Info = 2,
            Debug = 3,
            Verbose = 4,
            MAX = 5
        };

        public enum ReturnCode
        {
            SUCCESS = 0,
            SUCCESS_VERSION_OLD_SDK = 1001,
            SUCCESS_VERSION_OLD_GFE = 1002,
            SUCCESS_PENDING = 1003,
            SUCCESS_USER_NOT_INTERESTED = 1004,
            SUCCESS_PERMISSION_GRANTED = 1005,

            ERR_GENERIC = -1001,
            ERR_GFE_VERSION = -1002,
            ERR_SDK_VERSION = -1003,
            ERR_NOT_IMPLEMENTED = -1004,
            ERR_INVALID_PARAMETER = -1005,
            ERR_NOT_SET = -1006,
            ERR_SHADOWPLAY_IR_DISABLED = -1007,
            ERR_SDK_IN_USE = -1008,
            ERR_GROUP_NOT_FOUND = -1009,
            ERR_FILE_NOT_FOUND = -1010,
            ERR_HIGHLIGHTS_SETUP_FAILED = -1011,
            ERR_HIGHLIGHTS_NOT_CONFIGURED = -1012,
            ERR_HIGHLIGHTS_SAVE_FAILED = -1013,
            ERR_UNEXPECTED_EXCEPTION = -1014,
            ERR_NO_HIGHLIGHTS = -1015,
            ERR_NO_CONNECTION = -1016,
            ERR_PERMISSION_NOT_GRANTED = -1017,
            ERR_PERMISSION_DENIED = -1018,
            ERR_INVALID_HANDLE = -1019,
            ERR_UNHANDLED_EXCEPTION = -1020,
            ERR_OUT_OF_MEMORY = -1021,
            ERR_LOAD_LIBRARY = -1022,
            ERR_LIB_CALL_FAILED = -1023,
            ERR_IPC_FAILED = -1024,
            ERR_CONNECTION = -1025,
            ERR_MODULE_NOT_LOADED = -1026,
            ERR_LIB_CALL_TIMEOUT = -1027,
            ERR_APPLICATION_LOOKUP_FAILED = -1028,
            ERR_APPLICATION_NOT_KNOWN = -1029,
            ERR_FEATURE_DISABLED = -1030,
            ERR_APP_NO_OPTIMIZATION = -1031,
            ERR_APP_SETTINGS_READ = -1032,
            ERR_APP_SETTINGS_WRITE = -1033,
        };

        public struct TranslationEntry
        {
            public TranslationEntry(string _Language, string _Translation)
            {
                Language = _Language;
                Translation = _Translation;
            }
            public string Language;
            public string Translation;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct Scope
        {
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int value;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct HighlightDefinitionInternal
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string id;
            //[MarshalAsAttribute(UnmanagedType.I1)]
            public bool userDefaultInterest;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int highlightTags;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int significance;
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string languageTranslationStrings;
        };

        public struct HighlightDefinition
        {
            public string Id;
            public bool UserDefaultInterest;
            public HighlightType HighlightTags;
            public HighlightSignificance Significance;
            public TranslationEntry[] NameTranslationTable;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct OpenGroupParamsInternal
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string id;
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string groupDescriptionTable;
        };

        public struct OpenGroupParams
        {
            public string Id;
            public TranslationEntry[] GroupDescriptionTable;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct CloseGroupParams
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string id;
            //[MarshalAsAttribute(UnmanagedType.I1)]
            public bool destroyHighlights;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct ScreenshotHighlightParams
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string groupId;
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string highlightId;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct VideoHighlightParams
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string groupId;
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string highlightId;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int startDelta;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int endDelta;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct GroupViewInternal
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string groupId;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int tagFilter;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int significanceFilter;
        };

        public struct GroupView
        {
            public string GroupId;
            public HighlightType TagFilter;
            public HighlightSignificance SignificanceFilter;
        };

        public struct RequestPermissionsParams
        {
            public HighlightScope ScopesFlags;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct RequestPermissionsParamsInternal
        {
            //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 1)]
            public int scopesFlags;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct EmptyCallbackId
        {
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int id;
            //[MarshalAsAttribute(UnmanagedType.FunctionPtr)]
            public EmptyCallbackDelegate callback;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct GetNumberOfHighlightsCallbackId
        {
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int id;
            //[MarshalAsAttribute(UnmanagedType.FunctionPtr)]
            public GetNumberOfHighlightsCallbackDelegate callback;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct UserSettingsInternal
        {
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public IntPtr highlightSettingTable;
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int highlightSettingTableSize;
        };

        public struct UserSettings
        {
            public List<UserSetting> highlightSettingTable;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct UserSettingInternal
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string id;
            //[MarshalAsAttribute(UnmanagedType.I1)]
            public bool enabled;
        };

        public struct UserSetting
        {
            public string id;
            public bool enabled;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct GetUserSettingsCallbackId
        {
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int id;
            //[MarshalAsAttribute(UnmanagedType.FunctionPtr)]
            public GetUserSettingsCallbackDelegate callback;
            //[MarshalAsAttribute(UnmanagedType.FunctionPtr)]
            public IntermediateCallbackDelegateInternal intermediateCallback;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct UILanguageInternal
        {
            //[MarshalAsAttribute(UnmanagedType.LPStr)]
            public string cultureCode;
        };

        public struct UILanguage
        {
            public string cultureCode;
        };

        [StructLayout(LayoutKind.Sequential), Serializable]
        private struct GetUILanguageCallbackId
        {
            //[MarshalAsAttribute(UnmanagedType.SysInt)]
            public int id;
            //[MarshalAsAttribute(UnmanagedType.FunctionPtr)]
            public GetUILanguageCallbackDelegate callback;
            //[MarshalAsAttribute(UnmanagedType.FunctionPtr)]
            public IntermediateCallbackDelegateInternal intermediateCallback;
        };


        const string DLL64Name = "HighlightsPlugin64";

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Create([MarshalAs(UnmanagedType.LPStr)] string appName, int n, IntPtr scopes);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Release();

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_RequestPermissionsAsync(IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_GetUILanguageAsync(IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetDefaultLocale([MarshalAs(UnmanagedType.LPStr)] string defaultLocale);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_ConfigureAsync(int n, IntPtr highlightDefinitions, IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_GetUserSettingsAsync(IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_OpenGroupAsync(IntPtr openGroupParams, IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_CloseGroupAsync(IntPtr closeGroupParams, IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_SetScreenshotHighlightAsync(IntPtr screenshotHighlightParams, IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_SetVideoHighlightAsync(IntPtr videoHighlightParams, IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_OpenSummaryAsync(int n, IntPtr summaryParams, IntPtr asyncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Highlights_GetNumberOfHighlightsAsync(IntPtr groupView, IntPtr ayncOp);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.BStr)]
        public static extern string GetInfoLog();

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.BStr)]
        public static extern string GetErrorLog();

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Log_SetLevel(int level);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Log_AttachListener([MarshalAs(UnmanagedType.FunctionPtr)] LogListenerDelegate listener);

        [DllImport(DLL64Name, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Log_SetListenerLevel(int level);

        private static int CallbackId = 0;
        // For public use right before requesting an async operation, if CallbackId needs to be used to map a caller and a callback.
        public static int PeekCallbackId()
        {
            return CallbackId;
        }
        // Use privately once for one async operation request.
        private static int GetCallbackId()
        {
            return CallbackId++;
        }

        // Local functions
        //Constructs the main SDK interface. Also performs the version check.
        public static ReturnCode CreateHighlightsSDK(string AppName, HighlightScope[] RequiredScopes)
        {

            List<IntPtr> allocatedMemory = new List<IntPtr>();

            IntPtr nativeArray = Marshal.AllocHGlobal(RequiredScopes.Length * Marshal.SizeOf(typeof(IntPtr)));

            for (int i = 0; i < RequiredScopes.Length; ++i)
            {
                IntPtr nativeScope = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Scope)));
                allocatedMemory.Add(nativeScope);
                Scope scope = new Scope();
                scope.value = (int)RequiredScopes[i];
                Marshal.StructureToPtr(scope, nativeScope, false);
                Marshal.WriteIntPtr(nativeArray, i * Marshal.SizeOf(typeof(IntPtr)), nativeScope);
            }

            ReturnCode ret = (ReturnCode)Create(AppName, RequiredScopes.Length, nativeArray);

            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("Highlights SDK initialized successfully");
                instanceCreated = true;
            }
            else
            {
                Console.WriteLine("Failed to initialize Highlights SDK");
            }

            Marshal.FreeHGlobal(nativeArray);

            foreach (IntPtr ptr in allocatedMemory)
            {
                Marshal.FreeHGlobal(ptr);
            }

            return ret;
        }

        //Release the main SDK interface after create.
        public static void ReleaseHighlightsSDK()
        {
            if (!instanceCreated)
            {
                Console.WriteLine("Highlights release failed as no running instance was found");
                return;
            }

            if (Release())
                Console.WriteLine("Highlights SDK released successfully");
            else
                Console.WriteLine("Failed to release Highlights SDK");

            instanceCreated = false;

        }

        // Updates the unity log with info and error messages passed from the highlights sdk
        public static void UpdateLog()
        {
            string infoLog = GetInfoLog();
            if (infoLog != "")
            {
                Console.WriteLine(infoLog);
            }

            string errorLog = GetErrorLog();
            if (errorLog != "")
            {
                Console.WriteLine(errorLog);
            }
        }

        // Configure Highlights. Takes an array of highlight definition objects to define highlights in the game
        public static void ConfigureHighlights(HighlightDefinition[] highlightDefinitions, string defaultLocale, EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot configure Highlights. The SDK has not been initialized.");
                return;
            }

            SetDefaultLocale(defaultLocale);

            var allocatedMemory = new List<IntPtr>();

            IntPtr nativeArray = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * highlightDefinitions.Length);

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {

                for (int i = 0; i < highlightDefinitions.Length; ++i)
                {
                    IntPtr nativeHighlightsDefinition = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HighlightDefinitionInternal)));
                    HighlightDefinitionInternal hd = new HighlightDefinitionInternal();
                    hd.id = highlightDefinitions[i].Id;
                    hd.highlightTags = (int)(highlightDefinitions[i]).HighlightTags;
                    hd.significance = (int)(highlightDefinitions[i]).Significance;
                    hd.userDefaultInterest = (highlightDefinitions[i]).UserDefaultInterest;
                    StringBuilder sb = new StringBuilder();
                    foreach (TranslationEntry te in (highlightDefinitions[i]).NameTranslationTable)
                    {
                        sb.Append(te.Language).Append("\a").Append(te.Translation).Append("\a");
                    }
                    hd.languageTranslationStrings = sb.ToString();
                    allocatedMemory.Add(nativeHighlightsDefinition);
                    Marshal.StructureToPtr(hd, nativeHighlightsDefinition, false);
                    Marshal.WriteIntPtr(nativeArray, i * Marshal.SizeOf(typeof(IntPtr)), nativeHighlightsDefinition);
                }

                Marshal.StructureToPtr(cid, asyncOp, false);

                Highlights_ConfigureAsync(highlightDefinitions.Length, nativeArray, asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(nativeArray);
                Marshal.FreeHGlobal(asyncOp);

                foreach (IntPtr ptr in allocatedMemory)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        // Begins a "group" which groups several Highlights together.
        public static void OpenGroup(OpenGroupParams openGroupParams, EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot open a group. The SDK has not been initialized.");
                return;
            }

            OpenGroupParamsInternal ogp = new OpenGroupParamsInternal();
            ogp.id = openGroupParams.Id;

            StringBuilder sb = new StringBuilder();
            foreach (TranslationEntry te in openGroupParams.GroupDescriptionTable)
            {
                sb.Append(te.Language).Append("\a").Append(te.Translation).Append("\a");
            }

            ogp.groupDescriptionTable = sb.ToString();

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(ogp));

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(ogp, pnt, false);
                Marshal.StructureToPtr(cid, asyncOp, false);

                Highlights_OpenGroupAsync(pnt, asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
                Marshal.FreeHGlobal(asyncOp);
            }

        }

        // Closes out a group and purges the unsaved contents.
        public static void CloseGroup(CloseGroupParams closeGroupParams, EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot close a group. The SDK has not been initialized.");
                return;
            }

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(closeGroupParams));

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(closeGroupParams, pnt, false);
                Marshal.StructureToPtr(cid, asyncOp, false);

                Highlights_CloseGroupAsync(pnt, asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
                Marshal.FreeHGlobal(asyncOp);
            }
        }

        // Records a screenshot highlight for the given group. Attached metadata to it to make the Highlight more interesting.
        public static void SetScreenshotHighlight(ScreenshotHighlightParams screenshotHighlightParams, EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot take a screenshot. The SDK has not been initialized.");
                return;
            }

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(screenshotHighlightParams));

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(screenshotHighlightParams, pnt, false);
                Marshal.StructureToPtr(cid, asyncOp, false);

                Highlights_SetScreenshotHighlightAsync(pnt, asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
                Marshal.FreeHGlobal(asyncOp);
            }
        }

        // Records a video highlight for the given group. Attached metadata to it to make the Highlight more interesting.
        // Set the start and end delta to change the length of the video clip.
        public static void SetVideoHighlight(VideoHighlightParams videoHighlightParams, EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot record a video. The SDK has not been initialized.");
                return;
            }

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(videoHighlightParams));

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(videoHighlightParams, pnt, false);
                Marshal.StructureToPtr(cid, asyncOp, false);

                Highlights_SetVideoHighlightAsync(pnt, asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
                Marshal.FreeHGlobal(asyncOp);
            }
        }

        // Opens up Summary Dialog for one or more groups
        public static void OpenSummary(GroupView[] summaryParams, EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot open summary. The SDK has not been initialized.");
                return;
            }

            List<IntPtr> allocatedMemory = new List<IntPtr>();
            IntPtr nativeArray = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)) * summaryParams.Length);

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {

                for (int i = 0; i < summaryParams.Length; ++i)
                {
                    GroupViewInternal gvi = new GroupViewInternal();
                    gvi.groupId = summaryParams[i].GroupId;
                    gvi.significanceFilter = (int)(summaryParams[i]).SignificanceFilter;
                    gvi.tagFilter = (int)(summaryParams[i]).TagFilter;

                    IntPtr nativeSummaryParams = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GroupViewInternal)));
                    allocatedMemory.Add(nativeSummaryParams);
                    Marshal.StructureToPtr(gvi, nativeSummaryParams, false);
                    Marshal.WriteIntPtr(nativeArray, Marshal.SizeOf(typeof(IntPtr)) * i, nativeSummaryParams);
                }

                Marshal.StructureToPtr(cid, asyncOp, false);

                Highlights_OpenSummaryAsync(summaryParams.Length, nativeArray, asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(nativeArray);
                Marshal.FreeHGlobal(asyncOp);

                foreach (IntPtr ptr in allocatedMemory)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        public static void OpenSummary(GroupView[] summaryParams)
        {
            OpenSummary(summaryParams, DefaultOpenSummaryCallback);
        }

        // Retrieves the number of highlights given the group ID and filtering params
        public static void GetNumberOfHighlights(GroupView groupView, GetNumberOfHighlightsCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot get number of highlights. The SDK has not been initialized.");
                return;
            }

            GroupViewInternal spi = new GroupViewInternal();
            spi.groupId = groupView.GroupId;
            spi.significanceFilter = (int)groupView.SignificanceFilter;
            spi.tagFilter = (int)groupView.TagFilter;

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(spi));

            GetNumberOfHighlightsCallbackId cid = new GetNumberOfHighlightsCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr pntid = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(spi, pnt, false);
                Marshal.StructureToPtr(cid, pntid, false);
                Highlights_GetNumberOfHighlightsAsync(pnt, pntid);
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
                Marshal.FreeHGlobal(pntid);
            }
        }

        public delegate void GetNumberOfHighlightsCallbackDelegate(ReturnCode ret, int number, int id);

        private delegate void IntermediateCallbackDelegateInternal(ReturnCode ret, IntPtr blob, IntPtr callbackWithId);

        public delegate void EmptyCallbackDelegate(ReturnCode ret, int id);

        public static void GetUserSettings(GetUserSettingsCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot get highlights user setting. The SDK has not been initialized.");
                return;
            }

            GetUserSettingsCallbackId cid = new GetUserSettingsCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;
            cid.intermediateCallback = GetUserSettingsCallbackInternal;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(cid, asyncOp, false);
                Highlights_GetUserSettingsAsync(asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(asyncOp);
            }
        }

        private static void GetUserSettingsCallbackInternal(ReturnCode ret, IntPtr blob, IntPtr callbackId)
        {
            UserSettings uss = new UserSettings();
            uss.highlightSettingTable = new List<UserSetting>();

            // read from blob when possible
            if (ret == ReturnCode.SUCCESS)
            {
                UserSettingsInternal ussi = new UserSettingsInternal();
                ussi = (UserSettingsInternal)Marshal.PtrToStructure(blob, typeof(UserSettingsInternal));
                for (int i = 0; i < ussi.highlightSettingTableSize; i++)
                {
                    // read usi
                    UserSettingInternal usi = new UserSettingInternal();
                    IntPtr ptrOfOneSetting = new IntPtr(ussi.highlightSettingTable.ToInt64() + i * Marshal.SizeOf(usi));
                    usi = (UserSettingInternal)Marshal.PtrToStructure(ptrOfOneSetting, typeof(UserSettingInternal));

                    // copy usi to us
                    UserSetting us = new UserSetting();
                    us.enabled = usi.enabled;
                    us.id = usi.id;

                    // pack us into uss
                    uss.highlightSettingTable.Add(us);
                }
            }

            // read cid
            GetUserSettingsCallbackId cid = new GetUserSettingsCallbackId();

            cid = (GetUserSettingsCallbackId)Marshal.PtrToStructure(callbackId, typeof(GetUserSettingsCallbackId));
            GetUserSettingsCallbackDelegate nextCall = cid.callback;
            int id = cid.id;

            // call user provided function
            nextCall(ret, uss, id);
        }

        public delegate void GetUserSettingsCallbackDelegate(ReturnCode ret, UserSettings settings, int id);

        public static void GetUILanguage(GetUILanguageCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot get highlights UI language. The SDK has not been initialized.");
                return;
            }

            GetUILanguageCallbackId cid = new GetUILanguageCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;
            cid.intermediateCallback = GetUILanguageCallbackInternal;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(cid, asyncOp, false);
                Highlights_GetUILanguageAsync(asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(asyncOp);
            }
        }

        private static void GetUILanguageCallbackInternal(ReturnCode ret, IntPtr blob, IntPtr callbackId)
        {
            UILanguage ul = new UILanguage();

            // read from blob when possible
            if (ret == ReturnCode.SUCCESS)
            {
                // copy blob into uli
                UILanguageInternal uli = new UILanguageInternal();
                uli = (UILanguageInternal)Marshal.PtrToStructure(blob, typeof(UILanguageInternal));

                // read ul from uli
                ul.cultureCode = uli.cultureCode;
            }

            // read from cid
            GetUILanguageCallbackId cid = new GetUILanguageCallbackId();
            cid = (GetUILanguageCallbackId)Marshal.PtrToStructure(callbackId, typeof(GetUILanguageCallbackId));
            GetUILanguageCallbackDelegate nextCall = cid.callback;
            int id = cid.id;

            // call user provided function
            nextCall(ret, ul.cultureCode, id);
        }

        public delegate void GetUILanguageCallbackDelegate(ReturnCode ret, string cultureCode, int id);

        // Request permissions from the user (if not already granted to provide permissions for highlight capture)
        public static void RequestPermissions(EmptyCallbackDelegate callback)
        {
            if (!instanceCreated)
            {
                Console.WriteLine("ERROR: Cannot request permissions. The SDK has not been initialized.");
                return;
            }

            EmptyCallbackId cid = new EmptyCallbackId();
            cid.id = GetCallbackId();
            cid.callback = callback;

            IntPtr asyncOp = Marshal.AllocHGlobal(Marshal.SizeOf(cid));

            try
            {
                Marshal.StructureToPtr(cid, asyncOp, false);
                Highlights_RequestPermissionsAsync(asyncOp);
            }
            finally
            {
                Marshal.FreeHGlobal(asyncOp);
            }
        }

        public static ReturnCode SetLogLevel(LogLevel level)
        {
            return (ReturnCode)Log_SetLevel((int)level);
        }

        public static ReturnCode AttachLogListener(LogListenerDelegate listener)
        {
            return (ReturnCode)Log_AttachListener(listener);
        }

        public static ReturnCode SetListenerLogLevel(LogLevel level)
        {
            return (ReturnCode)Log_SetListenerLevel((int)level);
        }

        public delegate void LogListenerDelegate(LogLevel level, string message);
        public static void DefaultLogListener(LogLevel level, string message)
        {
            string[] levelString = { "None", "Error", "Info", "Debug", "Verbose" };
            Console.WriteLine("Highlights LogListener " + levelString[(int)level] + ": " + message);
        }


        public static void DefaultGetNumberOfHighlightsCallback(ReturnCode ret, int number, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("GetNumberOfHighlightsCallback " + id + " returns " + number);
            }
            else
            {
                Console.WriteLine("GetNumberOfHighlightsCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultGetUserSettingsCallback(ReturnCode ret, UserSettings settings, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("GetUserSettingsCallback " + id + " returns table count " + settings.highlightSettingTable.Count);
                foreach (var setting in settings.highlightSettingTable)
                {
                    Console.WriteLine("GetUserSettingsCallback " + id + " " + setting.id + " " + setting.enabled);
                }
            }
            else
            {
                Console.WriteLine("GetUserSettingsCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultGetUILanguageCallback(ReturnCode ret, string langueage, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("GetUILanguageCallback " + id + " returns " + langueage);
            }
            else
            {
                Console.WriteLine("GetUILanguageCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultRequestPermissionsCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("RequestPermissionsCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("RequestPermissionsCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultConfigureCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("ConfigureCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("ConfigureCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultSetScreenshotCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("SetScreenshotCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("SetScreenshotCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultSetVideoCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("SetVideoCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("SetVideoCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultOpenSummaryCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("OpenSummaryCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("OpenSummaryCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultOpenGroupCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("OpenGroupCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("OpenGroupCallback " + id + " returns unsuccess");
            }
        }

        public static void DefaultCloseGroupCallback(ReturnCode ret, int id)
        {
            if (ret == ReturnCode.SUCCESS)
            {
                Console.WriteLine("CloseGroupCallback " + id + " returns success");
            }
            else
            {
                Console.WriteLine("CloseGroupCallback " + id + " returns unsuccess");
            }
        }

    }
}