using ConcurrentCollections;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace MultiplayerARPG.MMO
{
    [ApiController]
    public partial class ApiController : ControllerBase
    {
        // TODO: I'm going to make in-memory database without Redis for now
        // In the future it may implements Redis
        // It's going to get some data from all tables but not every records
        // Just some records that players were requested
        private ConcurrentHashSet<string> cachedUsernames = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ConcurrentHashSet<string> cachedEmails = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ConcurrentHashSet<string> cachedCharacterNames = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ConcurrentHashSet<string> cachedGuildNames = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ConcurrentDictionary<string, string> cachedUserAccessToken = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, int> cachedUserGold = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, int> cachedUserCash = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, PlayerCharacterData> cachedUserCharacter = new ConcurrentDictionary<string, PlayerCharacterData>();
        private ConcurrentDictionary<string, SocialCharacterData> cachedSocialCharacter = new ConcurrentDictionary<string, SocialCharacterData>();
        private ConcurrentDictionary<string, ConcurrentDictionary<string, BuildingSaveData>> cachedBuilding = new ConcurrentDictionary<string, ConcurrentDictionary<string, BuildingSaveData>>();
        private ConcurrentDictionary<int, PartyData> cachedParty = new ConcurrentDictionary<int, PartyData>();
        private ConcurrentDictionary<int, GuildData> cachedGuild = new ConcurrentDictionary<int, GuildData>();
        private ConcurrentDictionary<StorageId, List<CharacterItem>> cachedStorageItems = new ConcurrentDictionary<StorageId, List<CharacterItem>>();
        private ConcurrentDictionary<StorageId, long> updatingStorages = new ConcurrentDictionary<StorageId, long>();

        private bool disableCacheReading;
        private GuildRoleData[] defaultGuildMemberRoles = new GuildRoleData[] {
            new GuildRoleData() { roleName = "Master", canInvite = true, canKick = true, canUseStorage = true },
            new GuildRoleData() { roleName = "Member 1", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 2", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 3", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 4", canInvite = false, canKick = false, canUseStorage = false },
            new GuildRoleData() { roleName = "Member 5", canInvite = false, canKick = false, canUseStorage = false },
        };
        private int[] guildExpTree = new int[0];

        public BaseDatabase Database { get; private set; } = null;

        private readonly ILogger<ApiController> _logger;

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ValidateUserLogin}")]
        public async UniTask<IActionResult> ValidateUserLogin(ValidateUserLoginReq request)
        {
            string userId = Database.ValidateUserLogin(request.Username, request.Password);
            if (string.IsNullOrEmpty(userId))
            {
                return StatusCode(401);
            }
            return Ok(new ValidateUserLoginResp()
            {
                UserId = userId,
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ValidateAccessToken}")]
        public async UniTask<IActionResult> ValidateAccessToken(ValidateAccessTokenReq request)
        {
            return Ok(new ValidateAccessTokenResp()
            {
                IsPass = ValidateAccessToken(request.UserId, request.AccessToken),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetUserLevel}")]
        public async UniTask<IActionResult> GetUserLevel(GetUserLevelReq request)
        {
            if (!Database.ValidateAccessToken(request.UserId, request.AccessToken))
            {
                return StatusCode(403);
            }
            return Ok(new GetUserLevelResp()
            {
                UserLevel = Database.GetUserLevel(request.UserId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetGold}")]
        public async UniTask<IActionResult> GetGold(GetGoldReq request)
        {
            return Ok(new GoldResp()
            {
                Gold = ReadGold(request.UserId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ChangeGold}")]
        public async UniTask<IActionResult> ChangeGold(ChangeGoldReq request)
        {
            int gold = ReadGold(request.UserId);
            gold += request.ChangeAmount;
            // Cache the data, it will be used later
            cachedUserGold[request.UserId] = gold;
            // Update data to database
            Database.UpdateGold(request.UserId, gold);
            return Ok(new GoldResp()
            {
                Gold = gold
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetCash}")]
        public async UniTask<IActionResult> GetCash(GetCashReq request)
        {
            return Ok(new CashResp()
            {
                Cash = ReadCash(request.UserId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ChangeCash}")]
        public async UniTask<IActionResult> ChangeCash(ChangeCashReq request)
        {
            int cash = ReadCash(request.UserId);
            cash += request.ChangeAmount;
            // Cache the data, it will be used later
            cachedUserCash[request.UserId] = cash;
            // Update data to database
            Database.UpdateCash(request.UserId, cash);
            return Ok(new CashResp()
            {
                Cash = cash
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateAccessToken}")]
        public async UniTask<IActionResult> UpdateAccessToken(UpdateAccessTokenReq request)
        {
            // Store access token to the dictionary, it will be used to validate later
            cachedUserAccessToken[request.UserId] = request.AccessToken;
            // Update data to database
            Database.UpdateAccessToken(request.UserId, request.AccessToken);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.CreateUserLogin}")]
        public async UniTask<IActionResult> CreateUserLogin(CreateUserLoginReq request)
        {
            // Cache username, it will be used to validate later
            cachedUsernames.Add(request.Username);
            // Insert new user login to database
            Database.CreateUserLogin(request.Username, request.Password, request.Email);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.FindUsername}")]
        public async UniTask<IActionResult> FindUsername(FindUsernameReq request)
        {
            return Ok(new FindUsernameResp()
            {
                FoundAmount = FindUsername(request.Username),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.CreateCharacter}")]
        public async UniTask<IActionResult> CreateCharacter(CreateCharacterReq request)
        {
            PlayerCharacterData character = request.CharacterData;
            // Insert new character to database
            Database.CreateCharacter(request.UserId, character);
            return Ok(new CharacterResp()
            {
                CharacterData = character
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadCharacter}")]
        public async UniTask<IActionResult> ReadCharacter(ReadCharacterReq request)
        {
            return Ok(new CharacterResp()
            {
                CharacterData = ReadCharacterWithUserIdValidation(request.CharacterId, request.UserId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadCharacters}")]
        public async UniTask<IActionResult> ReadCharacters(ReadCharactersReq request)
        {
            List<PlayerCharacterData> characters = Database.ReadCharacters(request.UserId);
            // Read and cache character (or load from cache)
            long lastUpdate;
            for (int i = 0; i < characters.Count; ++i)
            {
                lastUpdate = characters[i].LastUpdate;
                characters[i] = ReadCharacter(characters[i].Id);
                characters[i].LastUpdate = lastUpdate;
            }
            return Ok(new CharactersResp()
            {
                List = characters
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateCharacter}")]
        public async UniTask<IActionResult> UpdateCharacter(UpdateCharacterReq request)
        {
            PlayerCharacterData character = request.CharacterData;
            // Cache the data, it will be used later
            cachedUserCharacter[character.Id] = character;
            // Update data to database
            Database.UpdateCharacter(character);
            return Ok(new CharacterResp()
            {
                CharacterData = character
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.DeleteCharacter}")]
        public async UniTask<IActionResult> DeleteCharacter(DeleteCharacterReq request)
        {
            // Remove data from cache
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
            {
                string characterName = cachedUserCharacter[request.CharacterId].CharacterName;
                cachedCharacterNames.TryRemove(characterName);
                cachedUserCharacter.TryRemove(request.CharacterId, out _);
            }
            // Delete data from database
            Database.DeleteCharacter(request.UserId, request.CharacterId);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.FindCharacterName}")]
        public async UniTask<IActionResult> FindCharacterName(FindCharacterNameReq request)
        {
            return Ok(new FindCharacterNameResp()
            {
                FoundAmount = FindCharacterName(request.CharacterName),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.FindCharacters}")]
        public async UniTask<IActionResult> FindCharacters(FindCharacterNameReq request)
        {
            return Ok(new SocialCharactersResp()
            {
                List = Database.FindCharacters(request.FinderId, request.CharacterName, request.Skip, request.Limit)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.CreateFriend}")]
        public async UniTask<IActionResult> CreateFriend(CreateFriendReq request)
        {
            Database.CreateFriend(request.Character1Id, request.Character2Id, request.State);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.DeleteFriend}")]
        public async UniTask<IActionResult> DeleteFriend(DeleteFriendReq request)
        {
            Database.DeleteFriend(request.Character1Id, request.Character2Id);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadFriends}")]
        public async UniTask<IActionResult> ReadFriends(ReadFriendsReq request)
        {
            return Ok(new SocialCharactersResp()
            {
                List = Database.ReadFriends(request.CharacterId, request.ReadById2, request.State, request.Skip, request.Limit),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.CreateBuilding}")]
        public async UniTask<IActionResult> CreateBuilding(CreateBuildingReq request)
        {
            BuildingSaveData building = request.BuildingData;
            // Cache building data
            if (cachedBuilding.ContainsKey(request.MapName))
            {
                if (cachedBuilding[request.MapName].ContainsKey(building.Id))
                    cachedBuilding[request.MapName][building.Id] = building;
                else
                    cachedBuilding[request.MapName].TryAdd(building.Id, building);
            }
            // Insert data to database
            Database.CreateBuilding(request.MapName, building);
            return Ok(new BuildingResp()
            {
                BuildingData = request.BuildingData
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateBuilding}")]
        public async UniTask<IActionResult> UpdateBuilding(UpdateBuildingReq request)
        {
            BuildingSaveData building = request.BuildingData;
            // Cache building data
            if (cachedBuilding.ContainsKey(request.MapName))
            {
                if (cachedBuilding[request.MapName].ContainsKey(building.Id))
                    cachedBuilding[request.MapName][building.Id] = building;
                else
                    cachedBuilding[request.MapName].TryAdd(building.Id, building);
            }
            // Update data to database
            Database.UpdateBuilding(request.MapName, building);
            return Ok(new BuildingResp()
            {
                BuildingData = request.BuildingData
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.DeleteBuilding}")]
        public async UniTask<IActionResult> DeleteBuilding(DeleteBuildingReq request)
        {
            // Remove from cache
            if (cachedBuilding.ContainsKey(request.MapName))
                cachedBuilding[request.MapName].TryRemove(request.BuildingId, out _);
            // Remove from database
            Database.DeleteBuilding(request.MapName, request.BuildingId);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadBuildings}")]
        public async UniTask<IActionResult> ReadBuildings(ReadBuildingsReq request)
        {
            return Ok(new BuildingsResp()
            {
                List = ReadBuildings(request.MapName),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.CreateParty}")]
        public async UniTask<IActionResult> CreateParty(CreatePartyReq request)
        {
            // Insert to database
            int partyId = Database.CreateParty(request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            // Cached the data
            PartyData party = new PartyData(partyId, request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            cachedParty[partyId] = party;
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateParty}")]
        public async UniTask<IActionResult> UpdateParty(UpdatePartyReq request)
        {
            PartyData party = ReadParty(request.PartyId);
            if (party == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            party.Setting(request.ShareExp, request.ShareItem);
            cachedParty[request.PartyId] = party;
            // Update to database
            Database.UpdateParty(request.PartyId, request.ShareExp, request.ShareItem);
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdatePartyLeader}")]
        public async UniTask<IActionResult> UpdatePartyLeader(UpdatePartyLeaderReq request)
        {
            PartyData party = ReadParty(request.PartyId);
            if (party == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            party.SetLeader(request.LeaderCharacterId);
            cachedParty[request.PartyId] = party;
            // Update to database
            Database.UpdatePartyLeader(request.PartyId, request.LeaderCharacterId);
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.DeleteParty}")]
        public async UniTask<IActionResult> DeleteParty(DeletePartyReq request)
        {
            Database.DeleteParty(request.PartyId);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateCharacterParty}")]
        public async UniTask<IActionResult> UpdateCharacterParty(UpdateCharacterPartyReq request)
        {
            PartyData party = ReadParty(request.PartyId);
            if (party == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            SocialCharacterData character = request.SocialCharacterData;
            party.AddMember(character);
            cachedParty[request.PartyId] = party;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(character.id))
                cachedUserCharacter[character.id].PartyId = request.PartyId;
            // Update to database
            Database.UpdateCharacterParty(character.id, request.PartyId);
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ClearCharacterParty}")]
        public async UniTask<IActionResult> ClearCharacterParty(ClearCharacterPartyReq request)
        {
            PlayerCharacterData character = ReadCharacter(request.CharacterId);
            if (character == null)
            {
                return Ok();
            }
            PartyData party = ReadParty(character.PartyId);
            if (party == null)
            {
                return Ok();
            }
            // Update to cache
            party.RemoveMember(request.CharacterId);
            cachedParty[character.PartyId] = party;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
                cachedUserCharacter[request.CharacterId].PartyId = 0;
            // Update to database
            Database.UpdateCharacterParty(request.CharacterId, 0);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadParty}")]
        public async UniTask<IActionResult> ReadParty(ReadPartyReq request)
        {
            return Ok(new PartyResp()
            {
                PartyData = ReadParty(request.PartyId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.CreateGuild}")]
        public async UniTask<IActionResult> CreateGuild(CreateGuildReq request)
        {
            // Insert to database
            int guildId = Database.CreateGuild(request.GuildName, request.LeaderCharacterId);
            // Cached the data
            GuildData guild = new GuildData(guildId, request.GuildName, request.LeaderCharacterId, defaultGuildMemberRoles);
            cachedGuild[guildId] = guild;
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildLeader}")]
        public async UniTask<IActionResult> UpdateGuildLeader(UpdateGuildLeaderReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.SetLeader(request.LeaderCharacterId);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildLeader(request.GuildId, request.LeaderCharacterId);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildMessage}")]
        public async UniTask<IActionResult> UpdateGuildMessage(UpdateGuildMessageReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.guildMessage = request.GuildMessage;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMessage(request.GuildId, request.GuildMessage);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildMessage2}")]
        public async UniTask<IActionResult> UpdateGuildMessage2(UpdateGuildMessageReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.guildMessage2 = request.GuildMessage;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMessage2(request.GuildId, request.GuildMessage);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildScore}")]
        public async UniTask<IActionResult> UpdateGuildScore(UpdateGuildScoreReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.score = request.Score;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildScore(request.GuildId, request.Score);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildOptions}")]
        public async UniTask<IActionResult> UpdateGuildOptions(UpdateGuildOptionsReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.options = request.Options;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildOptions(request.GuildId, request.Options);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildAutoAcceptRequests}")]
        public async UniTask<IActionResult> UpdateGuildAutoAcceptRequests(UpdateGuildAutoAcceptRequestsReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.autoAcceptRequests = request.AutoAcceptRequests;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildAutoAcceptRequests(request.GuildId, request.AutoAcceptRequests);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildRank}")]
        public async UniTask<IActionResult> UpdateGuildRank(UpdateGuildRankReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.score = request.Rank;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildRank(request.GuildId, request.Rank);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildRole}")]
        public async UniTask<IActionResult> UpdateGuildRole(UpdateGuildRoleReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.SetRole(request.GuildRole, request.GuildRoleData);
            cachedGuild[request.GuildId] = guild;
            // Update to
            Database.UpdateGuildRole(request.GuildId, request.GuildRole, request.GuildRoleData);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateGuildMemberRole}")]
        public async UniTask<IActionResult> UpdateGuildMemberRole(UpdateGuildMemberRoleReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.SetMemberRole(request.MemberCharacterId, request.GuildRole);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMemberRole(request.MemberCharacterId, request.GuildRole);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.DeleteGuild}")]
        public async UniTask<IActionResult> DeleteGuild(DeleteGuildReq request)
        {
            // Remove data from cache
            if (cachedGuild.ContainsKey(request.GuildId))
            {
                string guildName = cachedGuild[request.GuildId].guildName;
                cachedGuildNames.TryRemove(guildName);
                cachedGuild.TryRemove(request.GuildId, out _);
            }
            // Remove data from database
            Database.DeleteGuild(request.GuildId);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateCharacterGuild}")]
        public async UniTask<IActionResult> UpdateCharacterGuild(UpdateCharacterGuildReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            SocialCharacterData character = request.SocialCharacterData;
            guild.AddMember(character, request.GuildRole);
            cachedGuild[request.GuildId] = guild;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(character.id))
                cachedUserCharacter[character.id].GuildId = request.GuildId;
            // Update to database
            Database.UpdateCharacterGuild(character.id, request.GuildId, request.GuildRole);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ClearCharacterGuild}")]
        public async UniTask<IActionResult> ClearCharacterGuild(ClearCharacterGuildReq request)
        {
            PlayerCharacterData character = ReadCharacter(request.CharacterId);
            if (character == null)
            {
                return Ok();
            }
            GuildData guild = ReadGuild(character.GuildId);
            if (guild == null)
            {
                return Ok();
            }
            // Update to cache
            guild.RemoveMember(request.CharacterId);
            cachedGuild[character.GuildId] = guild;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
            {
                cachedUserCharacter[request.CharacterId].GuildId = 0;
                cachedUserCharacter[request.CharacterId].GuildRole = 0;
            }
            // Update to database
            Database.UpdateCharacterGuild(request.CharacterId, 0, 0);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.FindGuildName}")]
        public async UniTask<IActionResult> FindGuildName(FindGuildNameReq request)
        {
            return Ok(new FindGuildNameResp()
            {
                FoundAmount = FindGuildName(request.GuildName),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadGuild}")]
        public async UniTask<IActionResult> ReadGuild(ReadGuildReq request)
        {
            return Ok(new GuildResp()
            {
                GuildData = ReadGuild(request.GuildId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.IncreaseGuildExp}")]
        public async UniTask<IActionResult> IncreaseGuildExp(IncreaseGuildExpReq request)
        {
            // TODO: May validate guild by character
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            guild.IncreaseGuildExp(guildExpTree, request.Exp);
            // Update to cache
            cachedGuild.TryAdd(guild.id, guild);
            // Update to database
            Database.UpdateGuildLevel(request.GuildId, guild.level, guild.exp, guild.skillPoint);
            return Ok(new GuildResp()
            {
                GuildData = ReadGuild(request.GuildId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.AddGuildSkill}")]
        public async UniTask<IActionResult> AddGuildSkill(AddGuildSkillReq request)
        {
            // TODO: May validate guild by character
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            guild.AddSkillLevel(request.SkillId);
            // Update to cache
            cachedGuild[guild.id] = guild;
            // Update to database
            Database.UpdateGuildSkillLevel(request.GuildId, request.SkillId, guild.GetSkillLevel(request.SkillId), guild.skillPoint);
            return Ok(new GuildResp()
            {
                GuildData = ReadGuild(request.GuildId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetGuildGold}")]
        public async UniTask<IActionResult> GetGuildGold(GetGuildGoldReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            return Ok(new GuildGoldResp()
            {
                GuildGold = guild.gold
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ChangeGuildGold}")]
        public async UniTask<IActionResult> ChangeGuildGold(ChangeGuildGoldReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to cache
            guild.gold += request.ChangeAmount;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildGold(request.GuildId, guild.gold);
            return Ok(new GuildGoldResp()
            {
                GuildGold = guild.gold
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ReadStorageItems}")]
        public async UniTask<IActionResult> ReadStorageItems(ReadStorageItemsReq request)
        {
            StorageId storageId = new StorageId(request.StorageType, request.StorageOwnerId);
            if (request.ReadForUpdate)
            {
                long time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (updatingStorages.TryGetValue(storageId, out long oldTime) && time - oldTime < 500)
                {
                    // Not allow to update yet
                    return StatusCode(400);
                }
                updatingStorages.TryRemove(storageId, out _);
                updatingStorages.TryAdd(storageId, time);
            }
            return Ok(new ReadStorageItemsResp()
            {
                StorageCharacterItems = ReadStorageItems(storageId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateStorageItems}")]
        public async UniTask<IActionResult> UpdateStorageItems(UpdateStorageItemsReq request)
        {
            StorageId storageId = new StorageId(request.StorageType, request.StorageOwnerId);
            long time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (updatingStorages.TryGetValue(storageId, out long oldTime) && time - oldTime >= 500)
            {
                // Timeout
                return StatusCode(400);
            }
            if (request.UpdateCharacterData)
            {
                PlayerCharacterData character = request.CharacterData;
                // Cache the data, it will be used later
                cachedUserCharacter[character.Id] = character;
                // Update data to database
                Database.UpdateCharacter(character);
            }
            // Cache the data, it will be used later
            cachedStorageItems[storageId] = request.StorageItems;
            // Update data to database
            Database.UpdateStorageItems(request.StorageType, request.StorageOwnerId, request.StorageItems);
            updatingStorages.TryRemove(storageId, out _);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.MailList}")]
        public async UniTask<IActionResult> MailList(MailListReq request)
        {
            return Ok(new MailListResp()
            {
                List = Database.MailList(request.UserId, request.OnlyNewMails)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateReadMailState}")]
        public async UniTask<IActionResult> UpdateReadMailState(UpdateReadMailStateReq request)
        {
            long updated = Database.UpdateReadMailState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_READ_NOT_ALLOWED
                });
            }
            return Ok(new UpdateReadMailStateResp()
            {
                Mail = Database.GetMail(request.MailId, request.UserId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateClaimMailItemsState}")]
        public async UniTask<IActionResult> UpdateClaimMailItemsState(UpdateClaimMailItemsStateReq request)
        {
            long updated = Database.UpdateClaimMailItemsState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_CLAIM_NOT_ALLOWED
                });
            }
            return Ok(new UpdateClaimMailItemsStateResp()
            {
                Mail = Database.GetMail(request.MailId, request.UserId)
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateDeleteMailState}")]
        public async UniTask<IActionResult> UpdateDeleteMailState(UpdateDeleteMailStateReq request)
        {
            long updated = Database.UpdateDeleteMailState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_DELETE_NOT_ALLOWED
                });
            }
            return Ok(new UpdateDeleteMailStateResp());
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.SendMail}")]
        public async UniTask<IActionResult> SendMail(SendMailReq request)
        {
            Mail mail = request.Mail;
            if (string.IsNullOrEmpty(mail.ReceiverId))
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_SEND_NO_RECEIVER
                });
            }
            long created = Database.CreateMail(mail);
            if (created <= 0)
            {
                return StatusCode(500, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_SEND_NOT_ALLOWED
                });
            }
            return Ok(new SendMailResp());
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetMail}")]
        public async UniTask<IActionResult> GetMail(GetMailReq request)
        {
            return Ok(new GetMailResp()
            {
                Mail = Database.GetMail(request.MailId, request.UserId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetMailNotification}")]
        public async UniTask<IActionResult> GetMailNotification(GetMailNotificationReq request)
        {
            return Ok(new GetMailNotificationResp()
            {
                NotificationCount = Database.GetMailNotification(request.UserId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetIdByCharacterName}")]
        public async UniTask<IActionResult> GetIdByCharacterName(GetIdByCharacterNameReq request)
        {
            return Ok(new GetIdByCharacterNameResp()
            {
                Id = Database.GetIdByCharacterName(request.CharacterName),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetUserIdByCharacterName}")]
        public async UniTask<IActionResult> GetUserIdByCharacterName(GetUserIdByCharacterNameReq request)
        {
            return Ok(new GetUserIdByCharacterNameResp()
            {
                UserId = Database.GetUserIdByCharacterName(request.CharacterName),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetUserUnbanTime}")]
        public async UniTask<IActionResult> GetUserUnbanTime(GetUserUnbanTimeReq request)
        {
            long unbanTime = Database.GetUserUnbanTime(request.UserId);
            return Ok(new GetUserUnbanTimeResp()
            {
                UnbanTime = unbanTime,
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.SetUserUnbanTimeByCharacterName}")]
        public async UniTask<IActionResult> SetUserUnbanTimeByCharacterName(SetUserUnbanTimeByCharacterNameReq request)
        {
            Database.SetUserUnbanTimeByCharacterName(request.CharacterName, request.UnbanTime);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.SetCharacterUnmuteTimeByName}")]
        public async UniTask<IActionResult> SetCharacterUnmuteTimeByName(SetCharacterUnmuteTimeByNameReq request)
        {
            Database.SetCharacterUnmuteTimeByName(request.CharacterName, request.UnmuteTime);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetSummonBuffs}")]
        public async UniTask<IActionResult> GetSummonBuffs(GetSummonBuffsReq request)
        {
            return Ok(new GetSummonBuffsResp()
            {
                SummonBuffs = Database.GetSummonBuffs(request.CharacterId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.SetSummonBuffs}")]
        public async UniTask<IActionResult> SetSummonBuffs(SetSummonBuffsReq request)
        {
            Database.SetSummonBuffs(request.CharacterId, request.SummonBuffs);
            return Ok();
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.FindEmail}")]
        public async UniTask<IActionResult> FindEmail(FindEmailReq request)
        {
            return Ok(new FindEmailResp()
            {
                FoundAmount = FindEmail(request.Email),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.ValidateEmailVerification}")]
        public async UniTask<IActionResult> ValidateEmailVerification(ValidateEmailVerificationReq request)
        {
            return Ok(new ValidateEmailVerificationResp()
            {
                IsPass = Database.ValidateEmailVerification(request.UserId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.GetFriendRequestNotification}")]
        public async UniTask<IActionResult> GetFriendRequestNotification(GetFriendRequestNotificationReq request)
        {
            return Ok(new GetFriendRequestNotificationResp()
            {
                NotificationCount = Database.GetFriendRequestNotification(request.CharacterId),
            });
        }
        
        [HttpPost][Route($"/api/{DatabaseApiPath.UpdateUserCount}")]
        public async UniTask<IActionResult> UpdateUserCount(UpdateUserCountReq request)
        {
            Database.UpdateUserCount(request.UserCount);
            return Ok();
        }

        protected bool ValidateAccessToken(string userId, string accessToken)
        {
            if (!disableCacheReading && cachedUserAccessToken.ContainsKey(userId))
            {
                // Already cached access token, so validate access token from cache
                return accessToken.Equals(cachedUserAccessToken[userId]);
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                if (Database.ValidateAccessToken(userId, accessToken))
                {
                    // Pass, store access token to the dictionary
                    cachedUserAccessToken[userId] = accessToken;
                    return true;
                }
            }
            return false;
        }

        protected long FindUsername(string username)
        {
            long foundAmount;
            if (!disableCacheReading && cachedUsernames.Contains(username))
            {
                // Already cached username, so validate username from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindUsername(username);
                // Cache username, it will be used to validate later
                if (foundAmount > 0)
                    cachedUsernames.Add(username);
            }
            return foundAmount;
        }

        protected long FindCharacterName(string characterName)
        {
            long foundAmount;
            if (!disableCacheReading && cachedCharacterNames.Contains(characterName))
            {
                // Already cached character name, so validate character name from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindCharacterName(characterName);
                // Cache character name, it will be used to validate later
                if (foundAmount > 0)
                    cachedCharacterNames.Add(characterName);
            }
            return foundAmount;
        }

        protected long FindGuildName(string guildName)
        {
            long foundAmount;
            if (!disableCacheReading && cachedGuildNames.Contains(guildName))
            {
                // Already cached username, so validate username from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindGuildName(guildName);
                // Cache guild name, it will be used to validate later
                if (foundAmount > 0)
                    cachedGuildNames.Add(guildName);
            }
            return foundAmount;
        }

        protected long FindEmail(string email)
        {
            long foundAmount;
            if (!disableCacheReading && cachedEmails.Contains(email))
            {
                // Already cached username, so validate username from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindEmail(email);
                // Cache username, it will be used to validate later
                if (foundAmount > 0)
                    cachedEmails.Add(email);
            }
            return foundAmount;
        }

        protected List<BuildingSaveData> ReadBuildings(string mapName)
        {
            List<BuildingSaveData> buildings = new List<BuildingSaveData>();
            if (!disableCacheReading && cachedBuilding.ContainsKey(mapName))
            {
                // Get buildings from cache
                buildings.AddRange(cachedBuilding[mapName].Values);
            }
            else
            {
                // Read buildings from database
                buildings.AddRange(Database.ReadBuildings(mapName));
                // Store buildings to cache
                if (cachedBuilding.TryAdd(mapName, new ConcurrentDictionary<string, BuildingSaveData>()))
                {
                    foreach (BuildingSaveData building in buildings)
                    {
                        cachedBuilding[mapName].TryAdd(building.Id, building);
                    }
                }
            }
            return buildings;
        }

        protected int ReadGold(string userId)
        {
            if (disableCacheReading || !cachedUserGold.TryGetValue(userId, out int gold))
            {
                // Doesn't cached yet, so get data from database and cache it
                gold = Database.GetGold(userId);
                cachedUserGold[userId] = gold;
            }
            return gold;
        }

        protected int ReadCash(string userId)
        {
            if (disableCacheReading || !cachedUserCash.TryGetValue(userId, out int cash))
            {
                // Doesn't cached yet, so get data from database and cache it
                cash = Database.GetCash(userId);
                cachedUserCash[userId] = cash;
            }
            return cash;
        }

        protected PlayerCharacterData ReadCharacter(string id)
        {
            if (disableCacheReading || !cachedUserCharacter.TryGetValue(id, out PlayerCharacterData character))
            {
                // Doesn't cached yet, so get data from database
                character = Database.ReadCharacter(id);
                // Cache the data, it will be used later
                if (character != null)
                {
                    cachedUserCharacter[id] = character;
                    cachedCharacterNames.Add(character.CharacterName);
                }
            }
            return character;
        }

        protected PlayerCharacterData ReadCharacterWithUserIdValidation(string id, string userId)
        {
            if (disableCacheReading || !cachedUserCharacter.TryGetValue(id, out PlayerCharacterData character))
            {
                // Doesn't cached yet, so get data from database
                character = Database.ReadCharacter(id);
                // Cache the data, it will be used later
                if (character != null)
                {
                    cachedUserCharacter[id] = character;
                    cachedCharacterNames.Add(character.CharacterName);
                }
            }
            if (character != null && character.UserId != userId)
                character = null;
            return character;
        }

        protected SocialCharacterData ReadSocialCharacter(string id)
        {
            if (disableCacheReading || !cachedSocialCharacter.TryGetValue(id, out SocialCharacterData character))
            {
                // Doesn't cached yet, so get data from database
                character = SocialCharacterData.Create(Database.ReadCharacter(id, false, false, false, false, false, false, false, false, false, false));
                // Cache the data
                cachedSocialCharacter[id] = character;
            }
            return character;
        }

        protected PartyData ReadParty(int id)
        {
            if (disableCacheReading || !cachedParty.TryGetValue(id, out PartyData party))
            {
                // Doesn't cached yet, so get data from database
                party = Database.ReadParty(id);
                // Cache the data
                if (party != null)
                {
                    cachedParty[id] = party;
                    CacheSocialCharacters(party.GetMembers());
                }
            }
            return party;
        }

        protected GuildData ReadGuild(int id)
        {
            if (disableCacheReading || !cachedGuild.TryGetValue(id, out GuildData guild))
            {
                // Doesn't cached yet, so get data from database
                guild = Database.ReadGuild(id, defaultGuildMemberRoles);
                // Cache the data
                if (guild != null)
                {
                    cachedGuild[id] = guild;
                    cachedGuildNames.Add(guild.guildName);
                    CacheSocialCharacters(guild.GetMembers());
                }
            }
            return guild;
        }

        protected List<CharacterItem> ReadStorageItems(StorageId storageId)
        {
            if (disableCacheReading || !cachedStorageItems.TryGetValue(storageId, out List<CharacterItem> storageItems))
            {
                // Doesn't cached yet, so get data from database
                storageItems = Database.ReadStorageItems(storageId.storageType, storageId.storageOwnerId);
                // Cache the data, it will be used later
                if (storageItems != null)
                    cachedStorageItems[storageId] = storageItems;
            }
            return storageItems;
        }

        protected void CacheSocialCharacters(IEnumerable<SocialCharacterData> socialCharacters)
        {
            foreach (SocialCharacterData socialCharacter in socialCharacters)
            {
                cachedSocialCharacter[socialCharacter.id] = socialCharacter;
            }
        }
    }
}
