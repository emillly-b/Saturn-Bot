﻿using Discord;
using Discord.WebSocket;
using GenericBot.CommandModules;
using GenericBot.Database;
using GenericBot.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GenericBot
{
    public static class Core
    {
        public static GlobalConfiguration GlobalConfig { get; private set; }
        public static DiscordShardedClient DiscordClient { get; private set; }
        public static List<Command> Commands { get; set; }
        public static Logger Logger { get; private set; }
        public static MongoEngine MongoEngine { get; private set; }

        private static List<GuildConfig> LoadedGuildConfigs;

        static Core()
        {
            // Load global configs
            GlobalConfig = new GlobalConfiguration().Load();
            Logger = new Logger();
            Commands = new List<Command>();
            LoadCommands(GlobalConfig.CommandsToExclude);
            MongoEngine = new MongoEngine();
            LoadedGuildConfigs = new List<GuildConfig>();

            // Configure Client
            DiscordClient = new DiscordShardedClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Verbose,
                AlwaysDownloadUsers = true,
                MessageCacheSize = 100,
            });
            DiscordClient.Log += Logger.LogClientMessage;
            DiscordClient.MessageReceived += MessageEventHandler.MessageRecieved;
            DiscordClient.GuildAvailable += GuildEventHandler.GuildLoaded;
        }

        private static void LoadCommands(List<string> CommandsToExclude = null)
        {
            Commands.Clear();
            Commands.AddRange(new InfoModule().Load());

            if (CommandsToExclude == null)
                return;
            Commands = Commands.Where(c => !CommandsToExclude.Contains(c.Name)).ToList();
        }

        public static bool CheckBlacklisted(ulong UserId) => GlobalConfig.BlacklistedIds.Contains(UserId);
        public static ulong GetCurrentUserId() => DiscordClient.CurrentUser.Id;
        public static ulong GetOwnerId() => DiscordClient.GetApplicationInfoAsync().Result.Owner.Id;
        public static string GetGlobalPrefix() => GlobalConfig.DefaultPrefix;
        public static string GetPrefix(ParsedCommand context)
        {
            if (context.Guild == null || !string.IsNullOrEmpty(GetGuildConfig(context.Guild.Id).Prefix))
                return GetGuildConfig(context.Guild.Id).Prefix;
            return GetGlobalPrefix();
        }
        public static bool CheckGlobalAdmin(ulong UserId) => GlobalConfig.GlobalAdminIds.Contains(UserId);
        public static SocketGuild GetGuid(ulong GuildId) => DiscordClient.GetGuild(GuildId);

        public static GuildConfig GetGuildConfig(ulong GuildId)
        {
            if (LoadedGuildConfigs.Any(c => c.Id == GuildId))
            {
                return LoadedGuildConfigs.Find(c => c.Id == GuildId);
            }
            else
            {
                LoadedGuildConfigs.Add(MongoEngine.GetGuildConfig(GuildId));
                return GetGuildConfig(GuildId); // Now that it's cached
            }
        }
        public static async Task<GuildConfig> SaveGuildConfig(GuildConfig guildConfig)
        {
            if (LoadedGuildConfigs.Any(c => c.Id == guildConfig.Id))
                LoadedGuildConfigs.RemoveAll(c => c.Id == guildConfig.Id);
            LoadedGuildConfigs.Add(guildConfig);

            return MongoEngine.SaveGuildConfig(guildConfig);
        }
    }
}
