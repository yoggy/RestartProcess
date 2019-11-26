using System;
using System.Collections.Generic;
using System.Configuration;

namespace RestartProcess
{
    class Config : ApplicationSettingsBase
    {
        [UserScopedSetting]
        public string TargetProcessPath
        {
            get
            {
                object obj = this["TargetProcessPath"];
                if (obj == null) return null;
                return (string)obj;
            }
            set
            {
                this["TargetProcessPath"] = value;
            }
        }

        [UserScopedSetting]
        public bool RunAutomaticallyAtStartup
        {
            get
            {
                object obj = this["RunAutomaticallyAtStartup"];
                if (obj == null) return false;
                return (bool)obj;
            }
            set
            {
                this["RunAutomaticallyAtStartup"] = value;
            }
        }

        public new void Reset()
        {
            TargetProcessPath = "";
            RunAutomaticallyAtStartup = false;
        }
    }
}
