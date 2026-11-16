class CfgPatches
{
    class UTF_HC_Join
    {
        name = "UTF Headless Client readiness";
        units[] = {};
        weapons[] = {};
        requiredVersion = 2.06;
        requiredAddons[] = {"A3_Functions_F"};
    };
};

class CfgFunctions
{
    class UTFHC
    {
        class Join
        {
            file = "\utf_hc_join";
            class init
            {
                preInit = 1;
            };
        };
    };
};
