using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


class Program
{
    private DiscordSocketClient _client;
    private readonly ConcurrentDictionary<int, IUserMessage> _trackedMessages = new();
    private readonly string _adminFile = "AdminUsers.txt";
    private int _messageIndex = 0;
    private static readonly Random random = new Random();
    private List<string> _adminUsers = new();
    private static string lastSentFile = null;

    [STAThread] // Required for OpenFileDialog to work correctly on STA thread
    static async Task Main(string[] args) => await new Program().MainAsync();

    public async Task MainAsync()
    {
        Console.Title = "Dexter Morgan -User Memory";
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged |
                             GatewayIntents.MessageContent |
                             GatewayIntents.GuildMessages |
                             GatewayIntents.DirectMessages |
                             GatewayIntents.GuildMembers
        });

        _client.Log += LogAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.Ready += OnReadyAsync;

        LoadAdminUsers();

        string token = "MTM1OTY5ODA2NjA1MTAzOTI4Mw.GWgrpi.b0eDXkyEgXW3UsPTOeDwFAlDeIubZql25vOlrg"; // Replace your bot token here
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _ = Task.Run(InputLoop);
        await Task.Delay(-1);
    }

    private void LoadAdminUsers()
    {
        if (File.Exists(_adminFile))
            _adminUsers = File.ReadAllLines(_adminFile).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        else
            _adminUsers = new List<string>();
    }

    private void SaveAdminUsers()
    {
        File.WriteAllLines(_adminFile, _adminUsers);
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[+] Bot is online as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        Console.ResetColor();

        foreach (var guild in _client.Guilds)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[+]Currently in server: {guild.Name}");
            Console.ResetColor();
        }

        return Task.CompletedTask;
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        int id = Interlocked.Increment(ref _messageIndex);
        if (message is IUserMessage userMessage)
            _trackedMessages[id] = userMessage;

        var content = message.Content;
        var mentionedUser = message.MentionedUsers.FirstOrDefault();
        bool isAdmin = _adminUsers.Any(u => u.StartsWith(message.Author.Username + " ["));

        if (message.Channel is IDMChannel)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DM {id}] {message.Author.Username}#{message.Author.Discriminator}: {content}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[MSG {id}] {message.Author.Username}#{message.Author.Discriminator} in {(message.Channel as SocketGuildChannel)?.Guild.Name}: {content}");

            if (content.Contains("Show Allowed Users", StringComparison.OrdinalIgnoreCase))
            {
                if (_adminUsers.Count == 0)
                    await message.Channel.SendMessageAsync("bruh No allowed users found in the list.");
                else
                {
                    string list = string.Join(Environment.NewLine, _adminUsers.Select(u => "- " + u));
                    await message.Channel.SendMessageAsync($"**Showing Trusted Users:**\n```\n{list}\n```");
                }
            }
            if (message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id))
            {
                string contentWithoutMention = message.Content.Replace($"<@{_client.CurrentUser.Id}>", "", StringComparison.OrdinalIgnoreCase)
                                                              .Replace($"<@!{_client.CurrentUser.Id}>", "", StringComparison.OrdinalIgnoreCase)
                                                              .Trim();

                if (string.IsNullOrWhiteSpace(contentWithoutMention) ||
                    Regex.IsMatch(contentWithoutMention, @"^(hello|hi|@Dexter Morgan)$", RegexOptions.IgnoreCase))
                {
                    await message.Channel.SendMessageAsync($"Hello {message.Author.Username}, AI Server Is Currently Down Wait for fix nigga");
                }
            }
            if (content.StartsWith("!Role", StringComparison.OrdinalIgnoreCase) && content.Contains("Role:", StringComparison.OrdinalIgnoreCase))
            {
                if (!isAdmin)
                {
                    await message.Channel.SendMessageAsync("Access denied.");
                }
                else if (mentionedUser != null)
                {
                    string username = mentionedUser.Username;
                    var roleMatch = Regex.Match(content, @"Role:\s*(\w+)", RegexOptions.IgnoreCase);
                    if (roleMatch.Success)
                    {
                        string role = roleMatch.Groups[1].Value;
                        string entry = $"{username} [{role}]";

                        if (!_adminUsers.Contains(entry))
                        {
                            _adminUsers.Add(entry);
                            SaveAdminUsers();
                            await message.Channel.SendMessageAsync($"**Added {username} as [{role}] to the allowed users list**");
                        }
                        else
                        {
                            await message.Channel.SendMessageAsync($"**{username} is already listed as [{role}]**");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("❌ Invalid Use !Role @username Role: Admin");
                    }
                }
                else
                {
                    await message.Channel.SendMessageAsync("❌ Invalid Use !Role @username Role: Admin");
                }
            }
            if (content.StartsWith("!RemoveRole", StringComparison.OrdinalIgnoreCase))
            {
                if (!isAdmin)
                {
                    await message.Channel.SendMessageAsync("❌ You do not have permission to use this command.");
                }
                else if (mentionedUser != null)
                {
                    string username = mentionedUser.Username;
                    var removed = _adminUsers.RemoveAll(u => u.StartsWith(username + " ["));
                    if (removed > 0)
                    {
                        SaveAdminUsers();
                        await message.Channel.SendMessageAsync($"✅ Removed all roles for {username} from allowed users list.");
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync($"⚠️ {username} was not found in the allowed users list.");
                    }
                }
                else
                {
                    await message.Channel.SendMessageAsync("❌ Invalid format. Use !RemoveRole @username");
                }
            }

            if (content.StartsWith("!Kill", StringComparison.OrdinalIgnoreCase))
            {
                if (mentionedUser != null)
                {
                    await message.Channel.SendMessageAsync($"🩸 Dexter Morgan slowly approaches {mentionedUser.Mention}... *whispers* 'Tonight's the night.' 🔪");
                    await Task.Delay(1500);
                    await message.Channel.SendMessageAsync($"💀 {mentionedUser.Mention} has been added to Dexter's slide collection.");
                }
                else
                {
                    await message.Channel.SendMessageAsync("❌ Use !Kill @username");
                }
            }

            if (content.StartsWith("!Slice", StringComparison.OrdinalIgnoreCase))
            {
                if (mentionedUser != null)
                {
                    await message.Channel.SendMessageAsync($"🔪 {mentionedUser.Mention} has been sliced and added to Dexter’s Slide Collection!");
                }
                else
                {
                    await message.Channel.SendMessageAsync("❌ Use !Slice @username");
                }
            }

            if (content.StartsWith("!rape", StringComparison.OrdinalIgnoreCase))
            {
                if (mentionedUser != null)
                {
                    await message.Channel.SendMessageAsync($"{mentionedUser.Mention} has been Raped and added to Dexter’s Rape Collection");
                }
                else
                {
                    await message.Channel.SendMessageAsync("❌ Use !rape @username");
                }
            }

            if (content.StartsWith("!help", StringComparison.OrdinalIgnoreCase))
            {
                string helpMessage =
                    "- 1. Show allowed users\n" +
                    "- 2. !slice @member\n" +
                    "- 3. !DarkPassenger\n" +
                    "- 4. !kill @member\n" + 
                    "- 5. !role @member Role:Trusted,Admin,Owner\n" +
                    "- 6. !RemoveRole @member\n" +
                    "- 7. !ban @member\n" +
                    "- 8. !kick @member\n" +
                    "- 9. !BotInfo\n" +
                    "- 0. !SendEdit  --- will send an random edit from Dexter or brain moser or other \n";

                await message.Channel.SendMessageAsync(helpMessage);
            }

            if (content.StartsWith("!SendEdit", StringComparison.OrdinalIgnoreCase))
            {
                string editFolder = Path.Combine(Directory.GetCurrentDirectory(), "edit");

                if (!Directory.Exists(editFolder))
                {
                    await message.Channel.SendMessageAsync("Can't find the 'edit' folder on the server.");
                    return;
                }

                var mp4Files = Directory.GetFiles(editFolder, "*.mp4", SearchOption.TopDirectoryOnly);

                if (mp4Files.Length == 0)
                {
                    await message.Channel.SendMessageAsync("No .mp4 files found in 'edit' folder.");
                    return;
                }

                string randomFile = null;
                // Try to avoid sending the same file twice in a row (up to 5 tries)
                for (int i = 0; i < 5; i++)
                {
                    var candidate = mp4Files[random.Next(mp4Files.Length)];
                    if (candidate != lastSentFile)
                    {
                        randomFile = candidate;
                        break;
                    }
                }
                // If after 5 tries still the same, just pick one anyway
                randomFile ??= mp4Files[random.Next(mp4Files.Length)];

                var filename = Path.GetFileName(randomFile);

                try
                {
                    using var stream = new FileStream(randomFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await message.Channel.SendFileAsync(stream, filename, $"Here's a random edit: {filename}");
                    lastSentFile = randomFile;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SendEdit] Failed to send file '{filename}': {ex.Message}");
                    // No user message, fail silently except console log
                }
            }
            if (content.StartsWith("!BotInfo", StringComparison.OrdinalIgnoreCase))
            {
                string helpMessage =
                    "```diff\r\n+ Dexter Morgan uses OpenAi gpt-3 and TinyLLaMA (1.1B) Running on a server with 1050 ti 4gb vram 200 gb ssd with Server storage of 140 gb```\n";

                await message.Channel.SendMessageAsync(helpMessage);
            }
            if (content.Equals("!DarkPassenger", StringComparison.OrdinalIgnoreCase))
            {
                await message.Channel.SendMessageAsync("🧠 *The Dark Passenger awakens... whispering for retribution in the silence of the night.*");
            }

            if (content.Equals("!Code", StringComparison.OrdinalIgnoreCase))
            {
                await message.Channel.SendMessageAsync("📜 **Harry's Code**:\n1. Never get caught.\n2. When in doubt, follow the code.\n3. There is no room for error.");
            }

            // MODERATION COMMANDS (Admins only)
            if (content.StartsWith("!Kick", StringComparison.OrdinalIgnoreCase))
            {
                if (!isAdmin)
                {
                    await message.Channel.SendMessageAsync("Access Denied.");
                    return;
                }

                if (mentionedUser is SocketGuildUser guildUser)
                {
                    await guildUser.KickAsync("Kicked by Dexter Morgan");
                    await message.Channel.SendMessageAsync($"{mentionedUser.Username} has been kicked.");
                }
                else
                {
                    await message.Channel.SendMessageAsync("Use !Kick @username inside a guild channel.");
                }
            }
            if (content.StartsWith("!Ban", StringComparison.OrdinalIgnoreCase))
            {
                if (!isAdmin)
                {
                    await message.Channel.SendMessageAsync("Access Denied.");
                    return;
                }

                if (message.Channel is SocketGuildChannel guildChannel && mentionedUser != null)
                {
                    var guildUser = guildChannel.Guild.GetUser(mentionedUser.Id);
                    if (guildUser != null)
                    {
                        await guildUser.BanAsync(1, "Banned by Dexter Morgan");
                        await message.Channel.SendMessageAsync($"{mentionedUser.Username} has been banned.");
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("User not found in guild.");
                    }
                }
                else
                {
                    await message.Channel.SendMessageAsync("Use !Ban @username inside a guild channel.");
                }
            }
        }

        Console.ResetColor();
    }
    private readonly Dictionary<string, string> _emojiMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "100", "💯" },
    { "check", "✅" },
    { "heart", "❤️" }
};

    private async Task InputLoop()
    {
        while (true)
        {
            string input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            // Upload file as reply: r [id] u
            if (Regex.IsMatch(input, @"^r\s+\d+\s+u$", RegexOptions.IgnoreCase))
            {
                var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    Console.WriteLine("[!] Invalid command format. Use: r [messageId] u");
                    continue;
                }

                if (!int.TryParse(parts[1], out int messageId))
                {
                    Console.WriteLine("[!] Invalid message ID.");
                    continue;
                }

                if (!_trackedMessages.TryGetValue(messageId, out IUserMessage targetMessage))
                {
                    Console.WriteLine($"[!] Message with ID {messageId} not found.");
                    continue;
                }

                Console.Write("Enter the full path of the file to upload: ");
                string selectedFile = Console.ReadLine()?.Trim('\"');

                if (string.IsNullOrEmpty(selectedFile) || !File.Exists(selectedFile))
                {
                    Console.WriteLine("[!] No file selected or file does not exist.");
                    continue;
                }

                var channel = targetMessage.Channel;
                try
                {
                    using var fs = new FileStream(selectedFile, FileMode.Open, FileAccess.Read);
                    var filename = Path.GetFileName(selectedFile);
                    Console.WriteLine($"[+] Uploading file '{filename}' as reply to message ID {messageId}...");

                    await channel.SendFileAsync(fs, filename, $"Replying with file to message ID {messageId}", messageReference: new MessageReference(targetMessage.Id));
                    Console.WriteLine("[+] File uploaded and sent");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error sending file: {ex.Message}");
                }
            }
            // React with limited emoji keywords: r [id] react [keyword]
            else if (Regex.IsMatch(input, @"^r\s+\d+\s+react\s+\S+$", RegexOptions.IgnoreCase))
            {
                var parts = input.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    Console.WriteLine("[!] Invalid command format. Use: r [messageId] react [keyword]");
                    continue;
                }

                if (!int.TryParse(parts[1], out int messageId))
                {
                    Console.WriteLine("[!] Invalid message ID.");
                    continue;
                }

                if (!_trackedMessages.TryGetValue(messageId, out IUserMessage targetMessage))
                {
                    Console.WriteLine($"[!] Message with ID {messageId} not found.");
                    continue;
                }

                string emojiKey = parts[3];

                if (!_emojiMap.TryGetValue(emojiKey, out string emoji))
                {
                    Console.WriteLine($"[!] Unknown emoji keyword '{emojiKey}'. Valid keywords: 100, check, heart");
                    continue;
                }

                try
                {
                    var emote = new Emoji(emoji);
                    await targetMessage.AddReactionAsync(emote);
                    Console.WriteLine($"[+] Reacted to message ID {messageId} with '{emoji}' ({emojiKey}).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error adding reaction: {ex.Message}");
                }
            }
            // Reply with text message: r [id] some message text
            else if (Regex.IsMatch(input, @"^r\s+\d+\s+.+$", RegexOptions.IgnoreCase))
            {
                var spaceIndex = input.IndexOf(' ');
                var secondSpaceIndex = input.IndexOf(' ', spaceIndex + 1);

                if (spaceIndex < 0 || secondSpaceIndex < 0)
                {
                    Console.WriteLine("[!] Invalid command format. Use: r [messageId] [text]");
                    continue;
                }

                var messageIdPart = input.Substring(spaceIndex + 1, secondSpaceIndex - spaceIndex - 1);
                var replyText = input.Substring(secondSpaceIndex + 1);

                if (!int.TryParse(messageIdPart, out int messageId))
                {
                    Console.WriteLine("[!] Invalid message ID.");
                    continue;
                }

                if (!_trackedMessages.TryGetValue(messageId, out IUserMessage targetMessage))
                {
                    Console.WriteLine($"[!] Message with ID {messageId} not found.");
                    continue;
                }

                try
                {
                    var channel = targetMessage.Channel;
                    await channel.SendMessageAsync(replyText, messageReference: new MessageReference(targetMessage.Id));
                    Console.WriteLine($"[+] Replied to message ID {messageId} with text.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Error sending reply: {ex.Message}");
                }
            }
            else if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[*] Exiting...");
                await _client.LogoutAsync();
                await _client.StopAsync();
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("[!] Unknown command.");
            }
        }
    }
}
