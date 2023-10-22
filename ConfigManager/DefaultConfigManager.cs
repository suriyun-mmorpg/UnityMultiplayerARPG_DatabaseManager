using Newtonsoft.Json;

namespace MultiplayerARPG.MMO
{
    public class DefaultConfigManager : IConfigManager
    {
        public static readonly GuildRoleData[] DefaultGuildMemberRoles = new GuildRoleData[] {
            new GuildRoleData() { roleName = "Master", canInvite = true, canKick = true, canUseStorage = true },
            new GuildRoleData() { roleName = "Member 1", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 2", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 3", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 4", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 5", canInvite = false, canKick = false, canUseStorage = false },
        };
        public static readonly int[] DefaultGuildExpTree = new int[0];
        private readonly SocialSystemSetting _socialSystemSetting;

        public DefaultConfigManager(ILogger<DefaultConfigManager> logger)
        {
            // Social System Setting
            bool configFileFound = false;
            string configFolder = "./Config";
            string configFilePath = configFolder + "/socialSystemSetting.json";
            SocialSystemSetting socialSystemSetting = new SocialSystemSetting()
            {
                GuildMemberRoles = DefaultGuildMemberRoles,
                GuildExpTree = DefaultGuildExpTree,
            };

            logger.LogInformation("Reading social system setting config file from " + configFilePath);
            if (File.Exists(configFilePath))
            {
                logger.LogInformation("Found social system setting config file");
                string dataAsJson = File.ReadAllText(configFilePath);
                SocialSystemSetting replacingConfig = JsonConvert.DeserializeObject<SocialSystemSetting>(dataAsJson);
                if (replacingConfig.GuildMemberRoles != null)
                    socialSystemSetting.GuildMemberRoles = replacingConfig.GuildMemberRoles;
                if (replacingConfig.GuildExpTree != null)
                    socialSystemSetting.GuildExpTree = replacingConfig.GuildExpTree;
                configFileFound = true;
            }

            if (!configFileFound)
            {
                // Write config file
                logger.LogInformation("Not found social system setting config file, creating a new one");
                if (!Directory.Exists(configFolder))
                    Directory.CreateDirectory(configFolder);
                File.WriteAllText(configFilePath, JsonConvert.SerializeObject(socialSystemSetting, Formatting.Indented));
            }

            _socialSystemSetting = socialSystemSetting;
        }

        public SocialSystemSetting GetSocialSystemSetting()
        {
            return _socialSystemSetting;
        }
    }
}
