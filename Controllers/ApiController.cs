using ConcurrentCollections;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MultiplayerARPG.MMO
{
#nullable disable
    [Authorize]
    [ApiController]
    public partial class ApiController : ControllerBase
    {
        protected readonly ConcurrentHashSet<string> _insertingCharacterNames = new ConcurrentHashSet<string>();
        protected readonly ConcurrentHashSet<string> _insertingGuildNames = new ConcurrentHashSet<string>();

        private readonly ILogger<ApiController> _logger;
        private readonly IConfigManager _configManager;
        public IDatabaseCache DatabaseCache { get; private set; }
        public IDatabase Database { get; private set; }
        public GuildRoleData[] GuildMemberRoles => _configManager.GetSocialSystemSetting().GuildMemberRoles;
        public int[] GuildExpTree => _configManager.GetSocialSystemSetting().GuildExpTree;

        public ApiController(
            ILogger<ApiController> logger,
            IConfigManager configManager,
            IDatabaseCache databaseCache,
            IDatabase database)
        {
            _logger = logger;
            _configManager = configManager;
            DatabaseCache = databaseCache;
            Database = database;
        }

        [HttpPost($"/{DatabaseApiPath.ValidateUserLogin}")]
        public async UniTask<IActionResult> ValidateUserLogin(ValidateUserLoginReq request)
        {
            return Ok(new ValidateUserLoginResp()
            {
                UserId = await Database.ValidateUserLogin(request.Username, request.Password),
            });
        }

        [HttpPost($"/{DatabaseApiPath.ValidateAccessToken}")]
        public async UniTask<IActionResult> ValidateAccessToken(ValidateAccessTokenReq request)
        {
            return Ok(new ValidateAccessTokenResp()
            {
                IsPass = await ValidateAccessToken(request.UserId, request.AccessToken),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetUserLevel}")]
        public async UniTask<IActionResult> GetUserLevel(GetUserLevelReq request)
        {
            return Ok(new GetUserLevelResp()
            {
                UserLevel = await Database.GetUserLevel(request.UserId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetGold}")]
        public async UniTask<IActionResult> GetGold(GetGoldReq request)
        {
            return Ok(new GoldResp()
            {
                Gold = await GetGold(request.UserId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.ChangeGold}")]
        public async UniTask<IActionResult> ChangeGold(ChangeGoldReq request)
        {
            return Ok(new GoldResp()
            {
                Gold = await Database.ChangeGold(request.UserId, request.ChangeAmount),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetCash}")]
        public async UniTask<IActionResult> GetCash(GetCashReq request)
        {
            return Ok(new CashResp()
            {
                Cash = await GetCash(request.UserId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.ChangeCash}")]
        public async UniTask<IActionResult> ChangeCash(ChangeCashReq request)
        {
            return Ok(new CashResp()
            {
                Cash = await Database.ChangeCash(request.UserId, request.ChangeAmount),
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateAccessToken}")]
        public async UniTask<IActionResult> UpdateAccessToken(UpdateAccessTokenReq request)
        {
            await Database.UpdateAccessToken(request.UserId, request.AccessToken);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.CreateUserLogin}")]
        public async UniTask<IActionResult> CreateUserLogin(CreateUserLoginReq request)
        {
            await Database.CreateUserLogin(request.Username, request.Password, request.Email);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.FindUsername}")]
        public async UniTask<IActionResult> FindUsername(FindUsernameReq request)
        {
            return Ok(new FindUsernameResp()
            {
                FoundAmount = await FindUsername(request.Username),
            });
        }

        [HttpPost($"/{DatabaseApiPath.CreateCharacter}")]
        public async UniTask<IActionResult> CreateCharacter(CreateCharacterReq request)
        {
            PlayerCharacterData character = request.CharacterData;
            if (_insertingCharacterNames.Contains(character.CharacterName))
            {
                return StatusCode(400, new CharacterResp());
            }
            _insertingCharacterNames.Add(character.CharacterName);
            long foundAmount = await FindCharacterName(character.CharacterName);
            if (foundAmount > 0)
            {
                return StatusCode(400, new CharacterResp());
            }
            // Insert new character to database
            await Database.CreateCharacter(request.UserId, character);
            _insertingCharacterNames.TryRemove(character.CharacterName);
            return Ok(new CharacterResp()
            {
                CharacterData = character
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetCharacter}")]
        public async UniTask<IActionResult> GetCharacter(GetCharacterReq request)
        {
            return Ok(new CharacterResp()
            {
                CharacterData = await GetCharacterWithUserIdValidation(request.CharacterId, request.UserId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetCharacters}")]
        public async UniTask<IActionResult> GetCharacters(GetCharactersReq request)
        {
            List<PlayerCharacterData> characters = await Database.GetCharacters(request.UserId);
            return Ok(new CharactersResp()
            {
                List = characters
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateCharacter}")]
        public async UniTask<IActionResult> UpdateCharacter(UpdateCharacterReq request)
        {
            PlayerCharacterData playerCharacter = await GetCharacter(request.CharacterData.Id);
            if (playerCharacter == null)
                return NotFound();
            await Database.UpdateCharacter(request.State, request.CharacterData, request.SummonBuffs, request.DeleteStorageReservation);
            List<UniTask> tasks = new List<UniTask>
            {
                DatabaseCache.SetPlayerCharacter(request.CharacterData),
                DatabaseCache.SetSummonBuffs(request.CharacterData.Id, request.SummonBuffs),
            };
            await UniTask.WhenAll(tasks);
            return Ok(new CharacterResp()
            {
                CharacterData = request.CharacterData,
            });
        }

        [HttpPost($"/{DatabaseApiPath.DeleteCharacter}")]
        public async UniTask<IActionResult> DeleteCharacter(DeleteCharacterReq request)
        {
            PlayerCharacterData playerCharacter = await GetCharacter(request.CharacterId);
            if (playerCharacter == null)
                return NotFound();
            // Delete data from database
            await Database.DeleteCharacter(request.UserId, request.CharacterId);
            // Remove data from cache
            if (playerCharacter != null)
            {
                await UniTask.WhenAll(
                    DatabaseCache.RemovePlayerCharacter(playerCharacter.Id),
                    DatabaseCache.RemoveSocialCharacter(playerCharacter.Id));
            }
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.FindCharacterName}")]
        public async UniTask<IActionResult> FindCharacterName(FindCharacterNameReq request)
        {
            return Ok(new FindCharacterNameResp()
            {
                FoundAmount = await FindCharacterName(request.CharacterName),
            });
        }

        [HttpPost($"/{DatabaseApiPath.FindCharacters}")]
        public async UniTask<IActionResult> FindCharacters(FindCharacterNameReq request)
        {
            return Ok(new SocialCharactersResp()
            {
                List = await Database.FindCharacters(request.FinderId, request.CharacterName, request.Skip, request.Limit)
            });
        }

        [HttpPost($"/{DatabaseApiPath.CreateFriend}")]
        public async UniTask<IActionResult> CreateFriend(CreateFriendReq request)
        {
            await Database.CreateFriend(request.Character1Id, request.Character2Id, request.State);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.DeleteFriend}")]
        public async UniTask<IActionResult> DeleteFriend(DeleteFriendReq request)
        {
            await Database.DeleteFriend(request.Character1Id, request.Character2Id);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.GetFriends}")]
        public async UniTask<IActionResult> ReadFriends(GetFriendsReq request)
        {
            return Ok(new SocialCharactersResp()
            {
                List = await Database.GetFriends(request.CharacterId, request.ReadById2, request.State, request.Skip, request.Limit),
            });
        }

        [HttpPost($"/{DatabaseApiPath.CreateBuilding}")]
        public async UniTask<IActionResult> CreateBuilding(CreateBuildingReq request)
        {
            BuildingSaveData building = request.BuildingData;
            // Insert data to database
            await Database.CreateBuilding(request.ChannelId, request.MapName, building);
            // Cache building data
            await DatabaseCache.SetBuilding(request.ChannelId, request.MapName, building);
            return Ok(new BuildingResp()
            {
                BuildingData = request.BuildingData
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateBuilding}")]
        public async UniTask<IActionResult> UpdateBuilding(UpdateBuildingReq request)
        {
            // Update data to database
            await Database.UpdateBuilding(request.ChannelId, request.MapName, request.BuildingData);
            // Cache building data
            await DatabaseCache.SetBuilding(request.ChannelId, request.MapName, request.BuildingData);
            return Ok(new BuildingResp()
            {
                BuildingData = request.BuildingData
            });
        }

        [HttpPost($"/{DatabaseApiPath.DeleteBuilding}")]
        public async UniTask<IActionResult> DeleteBuilding(DeleteBuildingReq request)
        {
            // Remove data from cache
            await DatabaseCache.RemoveBuilding(request.ChannelId, request.MapName, request.BuildingId);
            // Remove data from database
            await Database.DeleteBuilding(request.ChannelId, request.MapName, request.BuildingId);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.GetBuildings}")]
        public async UniTask<IActionResult> GetBuildings(GetBuildingsReq request)
        {
            return Ok(new BuildingsResp()
            {
                List = await GetBuildings(request.ChannelId, request.MapName),
            });
        }

        [HttpPost($"/{DatabaseApiPath.CreateParty}")]
        public async UniTask<IActionResult> CreateParty(CreatePartyReq request)
        {
            // Insert to database
            int partyId = await Database.CreateParty(request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            PartyData party = new PartyData(partyId, request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            // Cache the data, it will be used later
            await UniTask.WhenAll(
                DatabaseCache.SetParty(party),
                DatabaseCache.SetPlayerCharacterPartyId(request.LeaderCharacterId, partyId),
                DatabaseCache.SetSocialCharacterPartyId(request.LeaderCharacterId, partyId));
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateParty}")]
        public async UniTask<IActionResult> UpdateParty(UpdatePartyReq request)
        {
            PartyData party = await GetParty(request.PartyId);
            if (party == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateParty(request.PartyId, request.ShareExp, request.ShareItem);
            // Update to cache
            party.Setting(request.ShareExp, request.ShareItem);
            await DatabaseCache.SetParty(party);
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdatePartyLeader}")]
        public async UniTask<IActionResult> UpdatePartyLeader(UpdatePartyLeaderReq request)
        {
            PartyData party = await GetParty(request.PartyId);
            if (party == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdatePartyLeader(request.PartyId, request.LeaderCharacterId);
            // Update to cache
            party.SetLeader(request.LeaderCharacterId);
            await DatabaseCache.SetParty(party);
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        [HttpPost($"/{DatabaseApiPath.DeleteParty}")]
        public async UniTask<IActionResult> DeleteParty(DeletePartyReq request)
        {
            // Remove data from database
            await Database.DeleteParty(request.PartyId);
            // Remove data from cache
            await DatabaseCache.RemoveParty(request.PartyId);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.UpdateCharacterParty}")]
        public async UniTask<IActionResult> UpdateCharacterParty(UpdateCharacterPartyReq request)
        {
            PartyData party = await GetParty(request.PartyId);
            if (party == null)
            {
                return StatusCode(404);
            }
            SocialCharacterData character = request.SocialCharacterData;
            // Update to database
            await Database.UpdateCharacterParty(character.id, request.PartyId);
            // Update to cache
            party.AddMember(character);
            await UniTask.WhenAll(
                DatabaseCache.SetParty(party),
                DatabaseCache.SetPlayerCharacterPartyId(character.id, party.id),
                DatabaseCache.SetSocialCharacterPartyId(character.id, party.id));
            return Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        [HttpPost($"/{DatabaseApiPath.ClearCharacterParty}")]
        public async UniTask<IActionResult> ClearCharacterParty(ClearCharacterPartyReq request)
        {
            PlayerCharacterData character = await GetCharacter(request.CharacterId);
            if (character == null)
            {
                return Ok();
            }
            PartyData party = await GetParty(character.PartyId);
            if (party == null)
            {
                return Ok();
            }
            // Update to database
            await Database.UpdateCharacterParty(request.CharacterId, 0);
            // Update to cache
            party.RemoveMember(request.CharacterId);
            await UniTask.WhenAll(
                DatabaseCache.SetParty(party),
                DatabaseCache.SetPlayerCharacterPartyId(request.CharacterId, 0),
                DatabaseCache.SetSocialCharacterPartyId(request.CharacterId, 0));
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.GetParty}")]
        public async UniTask<IActionResult> ReadParty(GetPartyReq request)
        {
            if (request.ForceClearCache)
                await DatabaseCache.RemoveParty(request.PartyId);
            return Ok(new PartyResp()
            {
                PartyData = await GetParty(request.PartyId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.CreateGuild}")]
        public async UniTask<IActionResult> CreateGuild(CreateGuildReq request)
        {
            if (_insertingGuildNames.Contains(request.GuildName))
            {
                return StatusCode(400, new GuildResp());
            }
            _insertingGuildNames.Add(request.GuildName);
            long foundAmount = await FindGuildName(request.GuildName);
            if (foundAmount > 0)
            {
                return StatusCode(400, new GuildResp());
            }
            // Insert to database
            int guildId = await Database.CreateGuild(request.GuildName, request.LeaderCharacterId);
            GuildData guild = new GuildData(guildId, request.GuildName, request.LeaderCharacterId, GuildMemberRoles);
            // Cache the data, it will be used later
            await UniTask.WhenAll(
                DatabaseCache.SetGuild(guild),
                DatabaseCache.SetPlayerCharacterGuildIdAndRole(request.LeaderCharacterId, guildId, 0),
                DatabaseCache.SetSocialCharacterGuildIdAndRole(request.LeaderCharacterId, guildId, 0));
            _insertingGuildNames.TryRemove(request.GuildName);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildLeader}")]
        public async UniTask<IActionResult> UpdateGuildLeader(UpdateGuildLeaderReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildLeader(request.GuildId, request.LeaderCharacterId);
            // Update to cache
            guild.SetLeader(request.LeaderCharacterId);
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildMessage}")]
        public async UniTask<IActionResult> UpdateGuildMessage(UpdateGuildMessageReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildMessage(request.GuildId, request.GuildMessage);
            // Update to cache
            guild.guildMessage = request.GuildMessage;
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildMessage2}")]
        public async UniTask<IActionResult> UpdateGuildMessage2(UpdateGuildMessageReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildMessage2(request.GuildId, request.GuildMessage);
            // Update to cache
            guild.guildMessage2 = request.GuildMessage;
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildScore}")]
        public async UniTask<IActionResult> UpdateGuildScore(UpdateGuildScoreReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildScore(request.GuildId, request.Score);
            // Update to cache
            guild.score = request.Score;
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildOptions}")]
        public async UniTask<IActionResult> UpdateGuildOptions(UpdateGuildOptionsReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildOptions(request.GuildId, request.Options);
            // Update to cache
            guild.options = request.Options;
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildAutoAcceptRequests}")]
        public async UniTask<IActionResult> UpdateGuildAutoAcceptRequests(UpdateGuildAutoAcceptRequestsReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildAutoAcceptRequests(request.GuildId, request.AutoAcceptRequests);
            // Update to cache
            guild.autoAcceptRequests = request.AutoAcceptRequests;
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildRank}")]
        public async UniTask<IActionResult> UpdateGuildRank(UpdateGuildRankReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildRank(request.GuildId, request.Rank);
            // Update to cache
            guild.score = request.Rank;
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildRole}")]
        public async UniTask<IActionResult> UpdateGuildRole(UpdateGuildRoleReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildRole(request.GuildId, request.GuildRole, request.GuildRoleData);
            // Update to cache
            guild.SetRole(request.GuildRole, request.GuildRoleData);
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildMemberRole}")]
        public async UniTask<IActionResult> UpdateGuildMemberRole(UpdateGuildMemberRoleReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateGuildMemberRole(request.MemberCharacterId, request.GuildRole);
            // Update to cache
            guild.SetMemberRole(request.MemberCharacterId, request.GuildRole);
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.DeleteGuild}")]
        public async UniTask<IActionResult> DeleteGuild(DeleteGuildReq request)
        {
            // Remove data from database
            await Database.DeleteGuild(request.GuildId);
            // Remove data from cache
            await DatabaseCache.RemoveGuild(request.GuildId);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.UpdateCharacterGuild}")]
        public async UniTask<IActionResult> UpdateCharacterGuild(UpdateCharacterGuildReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            SocialCharacterData character = request.SocialCharacterData;
            // Update to database
            await Database.UpdateCharacterGuild(character.id, request.GuildId, request.GuildRole);
            // Update to cache
            guild.AddMember(character, request.GuildRole);
            await UniTask.WhenAll(
                DatabaseCache.SetGuild(guild),
                DatabaseCache.SetPlayerCharacterGuildIdAndRole(character.id, guild.id, request.GuildRole),
                DatabaseCache.SetSocialCharacterGuildIdAndRole(character.id, guild.id, request.GuildRole));
            return Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        [HttpPost($"/{DatabaseApiPath.ClearCharacterGuild}")]
        public async UniTask<IActionResult> ClearCharacterGuild(ClearCharacterGuildReq request)
        {
            PlayerCharacterData character = await GetCharacter(request.CharacterId);
            if (character == null)
            {
                return StatusCode(404);
            }
            GuildData guild = await GetGuild(character.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            // Update to database
            await Database.UpdateCharacterGuild(request.CharacterId, 0, 0);
            // Update to cache
            guild.RemoveMember(request.CharacterId);
            await UniTask.WhenAll(
                DatabaseCache.SetGuild(guild),
                DatabaseCache.SetPlayerCharacterGuildIdAndRole(request.CharacterId, 0, 0),
                DatabaseCache.SetSocialCharacterGuildIdAndRole(request.CharacterId, 0, 0));
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.FindGuildName}")]
        public async UniTask<IActionResult> FindGuildName(FindGuildNameReq request)
        {
            return Ok(new FindGuildNameResp()
            {
                FoundAmount = await FindGuildName(request.GuildName),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetGuild}")]
        public async UniTask<IActionResult> ReadGuild(GetGuildReq request)
        {
            if (request.ForceClearCache)
                await DatabaseCache.RemoveGuild(request.GuildId);
            return Ok(new GuildResp()
            {
                GuildData = await GetGuild(request.GuildId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.IncreaseGuildExp}")]
        public async UniTask<IActionResult> IncreaseGuildExp(IncreaseGuildExpReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            guild.IncreaseGuildExp(GuildExpTree, request.Exp);
            // Update to cache
            await DatabaseCache.SetGuild(guild);
            // Update to database
            await Database.UpdateGuildLevel(request.GuildId, guild.level, guild.exp, guild.skillPoint);
            return Ok(new GuildResp()
            {
                GuildData = await GetGuild(request.GuildId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.AddGuildSkill}")]
        public async UniTask<IActionResult> AddGuildSkill(AddGuildSkillReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            guild.AddSkillLevel(request.SkillId);
            // Update to database
            await Database.UpdateGuildSkillLevel(request.GuildId, request.SkillId, guild.GetSkillLevel(request.SkillId), guild.skillPoint);
            // Update to cache
            await DatabaseCache.SetGuild(guild);
            return Ok(new GuildResp()
            {
                GuildData = await GetGuild(request.GuildId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetGuildGold}")]
        public async UniTask<IActionResult> GetGuildGold(GetGuildGoldReq request)
        {
            GuildData guild = await GetGuild(request.GuildId);
            if (guild == null)
            {
                return StatusCode(404);
            }
            return Ok(new GuildGoldResp()
            {
                GuildGold = guild.gold
            });
        }

        [HttpPost($"/{DatabaseApiPath.ChangeGuildGold}")]
        public async UniTask<IActionResult> ChangeGuildGold(ChangeGuildGoldReq request)
        {
            // Update data to database
            int changedGuildGold = await Database.ChangeGuildGold(request.GuildId, request.ChangeAmount);
            // Cache the data, it will be used later
            DatabaseCacheResult<GuildData> getGuildResult = await DatabaseCache.GetGuild(request.GuildId);
            if (getGuildResult.HasValue)
            {
                GuildData guildData = getGuildResult.Value;
                guildData.gold = changedGuildGold;
                await DatabaseCache.SetGuild(guildData);
            }
            return Ok(new GuildGoldResp()
            {
                GuildGold = changedGuildGold,
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetStorageItems}")]
        public async UniTask<IActionResult> ReadStorageItems(GetStorageItemsReq request)
        {
            if (request.StorageType == StorageType.Guild)
            {
                if (await Database.FindReservedStorage(request.StorageType, request.StorageOwnerId) > 0)
                {
                    return StatusCode(400, new GetStorageItemsResp()
                    {
                        Error = UITextKeys.UI_ERROR_OTHER_GUILD_MEMBER_ACCESSING_STORAGE,
                    });
                }
                await Database.UpdateReservedStorage(request.StorageType, request.StorageOwnerId, request.ReserverId);
            }
            return Ok(new GetStorageItemsResp()
            {
                StorageItems = await GetStorageItems(request.StorageType, request.StorageOwnerId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateStorageItems}")]
        public async UniTask<IActionResult> UpdateStorageItems(UpdateStorageItemsReq request)
        {
            if (request.DeleteStorageReservation)
            {
                // Delete reserver
                await Database.DeleteReservedStorage(request.StorageType, request.StorageOwnerId);
            }
            // Update to database
            await Database.UpdateStorageItems(request.StorageType, request.StorageOwnerId, request.StorageItems);
            // Update to cache
            await DatabaseCache.SetStorageItems(request.StorageType, request.StorageOwnerId, request.StorageItems);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.UpdateStorageAndCharacterItems}")]
        public async UniTask<IActionResult> UpdateStorageAndCharacterItems(UpdateStorageAndCharacterItemsReq request)
        {
            if (request.DeleteStorageReservation)
            {
                // Delete reserver
                await Database.DeleteReservedStorage(request.StorageType, request.StorageOwnerId);
            }
            // Update to database
            await Database.UpdateStorageAndCharacterItems(
                request.StorageType,
                request.StorageOwnerId,
                request.StorageItems,
                request.CharacterId,
                request.SelectableWeaponSets,
                request.EquipItems,
                request.NonEquipItems);
            // Update to cache
            await UniTask.WhenAll(
                DatabaseCache.SetStorageItems(request.StorageType, request.StorageOwnerId, request.StorageItems),
                DatabaseCache.SetPlayerCharacterSelectableWeaponSets(request.CharacterId, request.SelectableWeaponSets),
                DatabaseCache.SetPlayerCharacterEquipItems(request.CharacterId, request.EquipItems),
                DatabaseCache.SetPlayerCharacterNonEquipItems(request.CharacterId, request.NonEquipItems));
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.DeleteAllReservedStorage}")]
        public async UniTask<IActionResult> DeleteAllReservedStorage()
        {
            await Database.DeleteAllReservedStorage();
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.MailList}")]
        public async UniTask<IActionResult> MailList(MailListReq request)
        {
            return Ok(new MailListResp()
            {
                List = await Database.MailList(request.UserId, request.OnlyNewMails)
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateReadMailState}")]
        public async UniTask<IActionResult> UpdateReadMailState(UpdateReadMailStateReq request)
        {
            long updated = await Database.UpdateReadMailState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_READ_NOT_ALLOWED
                });
            }
            return Ok(new UpdateReadMailStateResp()
            {
                Mail = await Database.GetMail(request.MailId, request.UserId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateClaimMailItemsState}")]
        public async UniTask<IActionResult> UpdateClaimMailItemsState(UpdateClaimMailItemsStateReq request)
        {
            long updated = await Database.UpdateClaimMailItemsState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_CLAIM_NOT_ALLOWED
                });
            }
            return Ok(new UpdateClaimMailItemsStateResp()
            {
                Mail = await Database.GetMail(request.MailId, request.UserId)
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateDeleteMailState}")]
        public async UniTask<IActionResult> UpdateDeleteMailState(UpdateDeleteMailStateReq request)
        {
            long updated = await Database.UpdateDeleteMailState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return StatusCode(400, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_DELETE_NOT_ALLOWED
                });
            }
            return Ok(new UpdateDeleteMailStateResp());
        }

        [HttpPost($"/{DatabaseApiPath.SendMail}")]
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
            long created = await Database.CreateMail(mail);
            if (created <= 0)
            {
                return StatusCode(500, new SendMailResp()
                {
                    Error = UITextKeys.UI_ERROR_MAIL_SEND_NOT_ALLOWED
                });
            }
            return Ok(new SendMailResp());
        }

        [HttpPost($"/{DatabaseApiPath.GetMail}")]
        public async UniTask<IActionResult> GetMail(GetMailReq request)
        {
            return Ok(new GetMailResp()
            {
                Mail = await Database.GetMail(request.MailId, request.UserId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetMailNotification}")]
        public async UniTask<IActionResult> GetMailNotification(GetMailNotificationReq request)
        {
            return Ok(new GetMailNotificationResp()
            {
                NotificationCount = await Database.GetMailNotification(request.UserId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetIdByCharacterName}")]
        public async UniTask<IActionResult> GetIdByCharacterName(GetIdByCharacterNameReq request)
        {
            return Ok(new GetIdByCharacterNameResp()
            {
                Id = await Database.GetIdByCharacterName(request.CharacterName),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetUserIdByCharacterName}")]
        public async UniTask<IActionResult> GetUserIdByCharacterName(GetUserIdByCharacterNameReq request)
        {
            return Ok(new GetUserIdByCharacterNameResp()
            {
                UserId = await Database.GetUserIdByCharacterName(request.CharacterName),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetUserUnbanTime}")]
        public async UniTask<IActionResult> GetUserUnbanTime(GetUserUnbanTimeReq request)
        {
            long unbanTime = await Database.GetUserUnbanTime(request.UserId);
            return Ok(new GetUserUnbanTimeResp()
            {
                UnbanTime = unbanTime,
            });
        }

        [HttpPost($"/{DatabaseApiPath.SetUserUnbanTimeByCharacterName}")]
        public async UniTask<IActionResult> SetUserUnbanTimeByCharacterName(SetUserUnbanTimeByCharacterNameReq request)
        {
            await Database.SetUserUnbanTimeByCharacterName(request.CharacterName, request.UnbanTime);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.SetCharacterUnmuteTimeByName}")]
        public async UniTask<IActionResult> SetCharacterUnmuteTimeByName(SetCharacterUnmuteTimeByNameReq request)
        {
            await Database.SetCharacterUnmuteTimeByName(request.CharacterName, request.UnmuteTime);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.GetSummonBuffs}")]
        public async UniTask<IActionResult> GetSummonBuffs(GetSummonBuffsReq request)
        {
            return Ok(new GetSummonBuffsResp()
            {
                SummonBuffs = await Database.GetSummonBuffs(request.CharacterId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.FindEmail}")]
        public async UniTask<IActionResult> FindEmail(FindEmailReq request)
        {
            return Ok(new FindEmailResp()
            {
                FoundAmount = await FindEmail(request.Email),
            });
        }

        [HttpPost($"/{DatabaseApiPath.ValidateEmailVerification}")]
        public async UniTask<IActionResult> ValidateEmailVerification(ValidateEmailVerificationReq request)
        {
            return Ok(new ValidateEmailVerificationResp()
            {
                IsPass = await Database.ValidateEmailVerification(request.UserId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetFriendRequestNotification}")]
        public async UniTask<IActionResult> GetFriendRequestNotification(GetFriendRequestNotificationReq request)
        {
            return Ok(new GetFriendRequestNotificationResp()
            {
                NotificationCount = await Database.GetFriendRequestNotification(request.CharacterId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateUserCount}")]
        public async UniTask<IActionResult> UpdateUserCount(UpdateUserCountReq request)
        {
            await Database.UpdateUserCount(request.UserCount);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.GetSocialCharacter}")]
        public async UniTask<IActionResult> ReadSocialCharacter(GetSocialCharacterReq request)
        {
            return Ok(new SocialCharacterResp()
            {
                SocialCharacterData = await GetSocialCharacter(request.CharacterId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.FindGuilds}")]
        public async UniTask<IActionResult> FindGuilds(FindGuildNameReq request)
        {
            return Ok(new GuildsResp()
            {
                List = await Database.FindGuilds(request.FinderId, request.GuildName, request.Skip, request.Limit)
            });
        }

        [HttpPost($"/{DatabaseApiPath.CreateGuildRequest}")]
        public async UniTask<IActionResult> CreateGuildRequest(CreateGuildRequestReq request)
        {
            await Database.CreateGuildRequest(request.GuildId, request.RequesterId);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.DeleteGuildRequest}")]
        public async UniTask<IActionResult> DeleteGuildRequest(DeleteGuildRequestReq request)
        {
            await Database.DeleteGuildRequest(request.GuildId, request.RequesterId);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.GetGuildRequests}")]
        public async UniTask<IActionResult> GetGuildRequests(GetGuildRequestsReq request)
        {
            return Ok(new SocialCharactersResp()
            {
                List = await Database.GetGuildRequests(request.GuildId, request.Skip, request.Limit)
            });
        }

        [HttpPost($"/{DatabaseApiPath.GetGuildRequestNotification}")]
        public async UniTask<IActionResult> GetGuildRequestNotification(GetGuildRequestNotificationReq request)
        {
            return Ok(new GetGuildRequestNotificationResp()
            {
                NotificationCount = await Database.GetGuildRequestsNotification(request.GuildId),
            });
        }

        [HttpPost($"/{DatabaseApiPath.UpdateGuildMemberCount}")]
        public async UniTask<IActionResult> UpdateGuildMemberCount(UpdateGuildMemberCountReq request)
        {
            await Database.UpdateGuildMemberCount(request.GuildId, request.MaxGuildMember);
            return Ok();
        }

        protected async UniTask<bool> ValidateAccessToken(string userId, string accessToken)
        {
            return await Database.ValidateAccessToken(userId, accessToken);
        }

        [HttpPost($"/{DatabaseApiPath.RemoveGuildCache}")]
        public async UniTask<IActionResult> RemoveGuildCache(GetGuildReq request)
        {
            await DatabaseCache.RemoveGuild(request.GuildId);
            return Ok();
        }

        [HttpPost($"/{DatabaseApiPath.RemovePartyCache}")]
        public async UniTask<IActionResult> RemovePartyCache(GetPartyReq request)
        {
            await DatabaseCache.RemoveParty(request.PartyId);
            return Ok();
        }

        protected async UniTask<long> FindUsername(string username)
        {
            return await Database.FindUsername(username);
        }

        protected async UniTask<long> FindCharacterName(string characterName)
        {
            return await Database.FindCharacterName(characterName);
        }

        protected async UniTask<long> FindGuildName(string guildName)
        {
            return await Database.FindGuildName(guildName);
        }

        protected async UniTask<long> FindEmail(string email)
        {
            return await Database.FindEmail(email);
        }

        protected async UniTask<List<BuildingSaveData>> GetBuildings(string channel, string mapName)
        {
            // Get buildings from cache
            var buildingsResult = await DatabaseCache.GetBuildings(channel, mapName);
            if (buildingsResult.HasValue)
                return new List<BuildingSaveData>(buildingsResult.Value);
            // Read buildings from database
            List<BuildingSaveData> buildings = await Database.GetBuildings(channel, mapName);
            if (buildings == null)
                buildings = new List<BuildingSaveData>();
            // Store buildings to cache
            await DatabaseCache.SetBuildings(channel, mapName, buildings);
            return buildings;
        }

        protected async UniTask<int> GetGold(string userId)
        {
            return await Database.GetGold(userId);
        }

        protected async UniTask<int> GetCash(string userId)
        {
            return await Database.GetCash(userId);
        }

        protected async UniTask<PlayerCharacterData> GetCharacter(string id)
        {
            // Get character from cache
            var characterResult = await DatabaseCache.GetPlayerCharacter(id);
            if (characterResult.HasValue)
            {
                return characterResult.Value;
            }
            // Read character from database
            PlayerCharacterData character = await Database.GetCharacter(id);
            if (character != null)
            {
                // Store character to cache
                await DatabaseCache.SetPlayerCharacter(character);
            }
            return character;
        }

        protected async UniTask<PlayerCharacterData> GetCharacterWithUserIdValidation(string id, string userId)
        {
            PlayerCharacterData character = await GetCharacter(id);
            if (character != null && character.UserId != userId)
                character = null;
            return character;
        }

        protected async UniTask<SocialCharacterData> GetSocialCharacter(string id)
        {
            // Get character from cache
            var characterResult = await DatabaseCache.GetSocialCharacter(id);
            if (characterResult.HasValue)
                return characterResult.Value;
            // Read character from database
            SocialCharacterData character = SocialCharacterData.Create(await Database.GetCharacter(id, false, false, false, false, false, false, false, false, false, false, false));
            // Store character to cache
            await DatabaseCache.SetSocialCharacter(character);
            return character;
        }

        protected async UniTask<PartyData> GetParty(int id)
        {
            // Get party from cache
            var partyResult = await DatabaseCache.GetParty(id);
            if (partyResult.HasValue)
                return partyResult.Value;
            // Read party from database
            PartyData party = await Database.GetParty(id);
            if (party != null)
            {
                // Store party to cache
                await UniTask.WhenAll(
                    DatabaseCache.SetParty(party),
                    CacheSocialCharacters(party.GetMembers()));
            }
            return party;
        }

        protected async UniTask<GuildData> GetGuild(int id)
        {
            // Get guild from cache
            var guildResult = await DatabaseCache.GetGuild(id);
            if (guildResult.HasValue)
                return guildResult.Value;
            // Read guild from database
            GuildData guild = await Database.GetGuild(id, GuildMemberRoles);
            if (guild != null)
            {
                // Store guild to cache
                await UniTask.WhenAll(
                    DatabaseCache.SetGuild(guild),
                    CacheSocialCharacters(guild.GetMembers()));
            }
            return guild;
        }

        protected async UniTask<List<CharacterItem>> GetStorageItems(StorageType storageType, string storageOwnerId)
        {
            // Get storageItems from cache
            var storageItemsResult = await DatabaseCache.GetStorageItems(storageType, storageOwnerId);
            if (storageItemsResult.HasValue)
                return new List<CharacterItem>(storageItemsResult.Value);
            // Read storageItems from database
            List<CharacterItem> storageItems = await Database.GetStorageItems(storageType, storageOwnerId);
            if (storageItems == null)
                storageItems = new List<CharacterItem>();
            // Store storageItems to cache
            await DatabaseCache.SetStorageItems(storageType, storageOwnerId, storageItems);
            return storageItems;
        }

        protected async UniTask CacheSocialCharacters(SocialCharacterData[] socialCharacters)
        {
            UniTask<bool>[] tasks = new UniTask<bool>[socialCharacters.Length];
            for (int i = 0; i < socialCharacters.Length; ++i)
            {
                tasks[i] = DatabaseCache.SetSocialCharacter(socialCharacters[i]);
            }
            await UniTask.WhenAll(tasks);
        }
    }
}
