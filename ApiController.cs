using ConcurrentCollections;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MultiplayerARPG.MMO
{
    public partial class ApiController
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

        public BaseDatabase Database { get; private set; }

        public ApiController(BaseDatabase database)
        {
            Database = database;
        }

        protected async UniTask<IResult> ValidateUserLogin(ValidateUserLoginReq request)
        {
            string userId = Database.ValidateUserLogin(request.Username, request.Password);
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Ok(new ValidateUserLoginResp());
            }
            return Results.Ok(new ValidateUserLoginResp()
            {
                UserId = userId,
            });
        }

        protected async UniTask<IResult> ValidateAccessToken(ValidateAccessTokenReq request)
        {
            return Results.Ok(new ValidateAccessTokenResp()
            {
                IsPass = ValidateAccessToken(request.UserId, request.AccessToken),
            });
        }

        protected async UniTask<IResult> GetUserLevel(GetUserLevelReq request)
        {
            if (!Database.ValidateAccessToken(request.UserId, request.AccessToken))
            {
                return Results.Unauthorized();
            }
            return Results.Ok(new GetUserLevelResp()
            {
                UserLevel = Database.GetUserLevel(request.UserId),
            });
        }

        protected async UniTask<IResult> GetGold(GetGoldReq request)
        {
            return Results.Ok(new GoldResp()
            {
                Gold = ReadGold(request.UserId)
            });
        }

        protected async UniTask<IResult> ChangeGold(ChangeGoldReq request)
        {
            int gold = ReadGold(request.UserId);
            gold += request.ChangeAmount;
            // Cache the data, it will be used later
            cachedUserGold[request.UserId] = gold;
            // Update data to database
            Database.UpdateGold(request.UserId, gold);
            return Results.Ok(new GoldResp()
            {
                Gold = gold
            });
        }

        protected async UniTask<IResult> GetCash(GetCashReq request)
        {
            return Results.Ok(new CashResp()
            {
                Cash = ReadCash(request.UserId)
            });
        }

        protected async UniTask<IResult> ChangeCash(ChangeCashReq request)
        {
            int cash = ReadCash(request.UserId);
            cash += request.ChangeAmount;
            // Cache the data, it will be used later
            cachedUserCash[request.UserId] = cash;
            // Update data to database
            Database.UpdateCash(request.UserId, cash);
            return Results.Ok(new CashResp()
            {
                Cash = cash
            });
        }

        protected async UniTask<IResult> UpdateAccessToken(UpdateAccessTokenReq request)
        {
            // Store access token to the dictionary, it will be used to validate later
            cachedUserAccessToken[request.UserId] = request.AccessToken;
            // Update data to database
            Database.UpdateAccessToken(request.UserId, request.AccessToken);
            return Results.Ok();
        }

        protected async UniTask<IResult> CreateUserLogin(CreateUserLoginReq request)
        {
            // Cache username, it will be used to validate later
            cachedUsernames.Add(request.Username);
            // Insert new user login to database
            Database.CreateUserLogin(request.Username, request.Password, request.Email);
            return Results.Ok();
        }

        protected async UniTask<IResult> FindUsername(FindUsernameReq request)
        {
            return Results.Ok(new FindUsernameResp()
            {
                FoundAmount = FindUsername(request.Username),
            });
        }

        protected async UniTask<IResult> CreateCharacter(CreateCharacterReq request)
        {
            PlayerCharacterData character = request.CharacterData;
            // Insert new character to database
            Database.CreateCharacter(request.UserId, character);
            return Results.Ok(new CharacterResp()
            {
                CharacterData = character
            });
        }

        protected async UniTask<IResult> ReadCharacter(ReadCharacterReq request)
        {
            return Results.Ok(new CharacterResp()
            {
                CharacterData = ReadCharacterWithUserIdValidation(request.CharacterId, request.UserId),
            });
        }

        protected async UniTask<IResult> ReadCharacters(ReadCharactersReq request)
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
            return Results.Ok(new CharactersResp()
            {
                List = characters
            });
        }

        protected async UniTask<IResult> UpdateCharacter(UpdateCharacterReq request)
        {
            PlayerCharacterData character = request.CharacterData;
            // Cache the data, it will be used later
            cachedUserCharacter[character.Id] = character;
            // Update data to database
            Database.UpdateCharacter(character);
            return Results.Ok(new CharacterResp()
            {
                CharacterData = character
            });
        }

        protected async UniTask<IResult> DeleteCharacter(DeleteCharacterReq request)
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
            return Results.Ok();
        }

        protected async UniTask<IResult> FindCharacterName(FindCharacterNameReq request)
        {
            return Results.Ok(new FindCharacterNameResp()
            {
                FoundAmount = FindCharacterName(request.CharacterName),
            });
        }

        protected async UniTask<IResult> FindCharacters(FindCharacterNameReq request)
        {
            return Results.Ok(new SocialCharactersResp()
            {
                List = Database.FindCharacters(request.FinderId, request.CharacterName, request.Skip, request.Limit)
            });
        }

        protected async UniTask<IResult> CreateFriend(CreateFriendReq request)
        {
            Database.CreateFriend(request.Character1Id, request.Character2Id, request.State);
            return Results.Ok();
        }

        protected async UniTask<IResult> DeleteFriend(DeleteFriendReq request)
        {
            Database.DeleteFriend(request.Character1Id, request.Character2Id);
            return Results.Ok();
        }

        protected async UniTask<IResult> ReadFriends(ReadFriendsReq request)
        {
            return Results.Ok(new SocialCharactersResp()
            {
                List = Database.ReadFriends(request.CharacterId, request.ReadById2, request.State, request.Skip, request.Limit),
            });
        }

        protected async UniTask<IResult> CreateBuilding(CreateBuildingReq request)
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
            return Results.Ok(new BuildingResp()
            {
                BuildingData = request.BuildingData
            });
        }

        protected async UniTask<IResult> UpdateBuilding(UpdateBuildingReq request)
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
            return Results.Ok(new BuildingResp()
            {
                BuildingData = request.BuildingData
            });
        }

        protected async UniTask<IResult> DeleteBuilding(DeleteBuildingReq request)
        {
            // Remove from cache
            if (cachedBuilding.ContainsKey(request.MapName))
                cachedBuilding[request.MapName].TryRemove(request.BuildingId, out _);
            // Remove from database
            Database.DeleteBuilding(request.MapName, request.BuildingId);
            return Results.Ok();
        }

        protected async UniTask<IResult> ReadBuildings(ReadBuildingsReq request)
        {
            return Results.Ok(new BuildingsResp()
            {
                List = ReadBuildings(request.MapName),
            });
        }

        protected async UniTask<IResult> CreateParty(CreatePartyReq request)
        {
            // Insert to database
            int partyId = Database.CreateParty(request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            // Cached the data
            PartyData party = new PartyData(partyId, request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            cachedParty[partyId] = party;
            return Results.Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        protected async UniTask<IResult> UpdateParty(UpdatePartyReq request)
        {
            PartyData party = ReadParty(request.PartyId);
            if (party == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            party.Setting(request.ShareExp, request.ShareItem);
            cachedParty[request.PartyId] = party;
            // Update to database
            Database.UpdateParty(request.PartyId, request.ShareExp, request.ShareItem);
            return Results.Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        protected async UniTask<IResult> UpdatePartyLeader(UpdatePartyLeaderReq request)
        {
            PartyData party = ReadParty(request.PartyId);
            if (party == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            party.SetLeader(request.LeaderCharacterId);
            cachedParty[request.PartyId] = party;
            // Update to database
            Database.UpdatePartyLeader(request.PartyId, request.LeaderCharacterId);
            return Results.Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        protected async UniTask<IResult> DeleteParty(DeletePartyReq request)
        {
            Database.DeleteParty(request.PartyId);
            return Results.Ok();
        }

        protected async UniTask<IResult> UpdateCharacterParty(UpdateCharacterPartyReq request)
        {
            PartyData party = ReadParty(request.PartyId);
            if (party == null)
            {
                return Results.NotFound();
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
            return Results.Ok(new PartyResp()
            {
                PartyData = party
            });
        }

        protected async UniTask<IResult> ClearCharacterParty(ClearCharacterPartyReq request)
        {
            PlayerCharacterData character = ReadCharacter(request.CharacterId);
            if (character == null)
            {
                return Results.Ok();
            }
            PartyData party = ReadParty(character.PartyId);
            if (party == null)
            {
                return Results.Ok();
            }
            // Update to cache
            party.RemoveMember(request.CharacterId);
            cachedParty[character.PartyId] = party;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
                cachedUserCharacter[request.CharacterId].PartyId = 0;
            // Update to database
            Database.UpdateCharacterParty(request.CharacterId, 0);
            return Results.Ok();
        }

        protected async UniTask<IResult> ReadParty(ReadPartyReq request)
        {
            return Results.Ok(new PartyResp()
            {
                PartyData = ReadParty(request.PartyId)
            });
        }

        protected async UniTask<IResult> CreateGuild(CreateGuildReq request)
        {
            // Insert to database
            int guildId = Database.CreateGuild(request.GuildName, request.LeaderCharacterId);
            // Cached the data
            GuildData guild = new GuildData(guildId, request.GuildName, request.LeaderCharacterId, request.Roles.ToArray());
            cachedGuild[guildId] = guild;
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildLeader(UpdateGuildLeaderReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.SetLeader(request.LeaderCharacterId);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildLeader(request.GuildId, request.LeaderCharacterId);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildMessage(UpdateGuildMessageReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.guildMessage = request.GuildMessage;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMessage(request.GuildId, request.GuildMessage);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildMessage2(UpdateGuildMessageReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.guildMessage2 = request.GuildMessage;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMessage2(request.GuildId, request.GuildMessage);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildScore(UpdateGuildScoreReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.score = request.Score;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildScore(request.GuildId, request.Score);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildOptions(UpdateGuildOptionsReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.options = request.Options;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildOptions(request.GuildId, request.Options);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildAutoAcceptRequests(UpdateGuildAutoAcceptRequestsReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.autoAcceptRequests = request.AutoAcceptRequests;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildAutoAcceptRequests(request.GuildId, request.AutoAcceptRequests);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildRank(UpdateGuildRankReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.score = request.Rank;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildRank(request.GuildId, request.Rank);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildRole(UpdateGuildRoleReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.SetRole(request.GuildRole, request.GuildRoleData);
            cachedGuild[request.GuildId] = guild;
            // Update to
            Database.UpdateGuildRole(request.GuildId, request.GuildRole, request.GuildRoleData);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> UpdateGuildMemberRole(UpdateGuildMemberRoleReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.SetMemberRole(request.MemberCharacterId, request.GuildRole);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMemberRole(request.MemberCharacterId, request.GuildRole);
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> DeleteGuild(DeleteGuildReq request)
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
            return Results.Ok();
        }

        protected async UniTask<IResult> UpdateCharacterGuild(UpdateCharacterGuildReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
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
            return Results.Ok(new GuildResp()
            {
                GuildData = guild
            });
        }

        protected async UniTask<IResult> ClearCharacterGuild(ClearCharacterGuildReq request)
        {
            PlayerCharacterData character = ReadCharacter(request.CharacterId);
            if (character == null)
            {
                return Results.Ok();
            }
            GuildData guild = ReadGuild(character.GuildId);
            if (guild == null)
            {
                return Results.Ok();
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
            return Results.Ok();
        }

        protected async UniTask<IResult> FindGuildName(FindGuildNameReq request)
        {
            return Results.Ok(new FindGuildNameResp()
            {
                FoundAmount = FindGuildName(request.GuildName),
            });
        }

        protected async UniTask<IResult> ReadGuild(ReadGuildReq request)
        {
            return Results.Ok(new GuildResp()
            {
                GuildData = ReadGuild(request.GuildId)
            });
        }

        protected async UniTask<IResult> IncreaseGuildExp(IncreaseGuildExpReq request)
        {
            // TODO: May validate guild by character
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            guild.level = request.Level;
            guild.exp = request.Exp;
            guild.skillPoint = request.SkillPoint;
            // Update to cache
            cachedGuild.TryAdd(guild.id, guild);
            // Update to database
            Database.UpdateGuildLevel(request.GuildId, guild.level, guild.exp, guild.skillPoint);
            return Results.Ok(new GuildResp()
            {
                GuildData = ReadGuild(request.GuildId)
            });
        }

        protected async UniTask<IResult> AddGuildSkill(AddGuildSkillReq request)
        {
            // TODO: May validate guild by character
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            guild.AddSkillLevel(request.SkillId);
            // Update to cache
            cachedGuild[guild.id] = guild;
            // Update to database
            Database.UpdateGuildSkillLevel(request.GuildId, request.SkillId, guild.GetSkillLevel(request.SkillId), guild.skillPoint);
            return Results.Ok(new GuildResp()
            {
                GuildData = ReadGuild(request.GuildId)
            });
        }

        protected async UniTask<IResult> GetGuildGold(GetGuildGoldReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            return Results.Ok(new GuildGoldResp()
            {
                GuildGold = guild.gold
            });
        }

        protected async UniTask<IResult> ChangeGuildGold(ChangeGuildGoldReq request)
        {
            GuildData guild = ReadGuild(request.GuildId);
            if (guild == null)
            {
                return Results.NotFound();
            }
            // Update to cache
            guild.gold += request.ChangeAmount;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildGold(request.GuildId, guild.gold);
            return Results.Ok(new GuildGoldResp()
            {
                GuildGold = guild.gold
            });
        }

        protected async UniTask<IResult> ReadStorageItems(ReadStorageItemsReq request)
        {
            StorageId storageId = new StorageId(request.StorageType, request.StorageOwnerId);
            if (request.ReadForUpdate)
            {
                long time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (updatingStorages.TryGetValue(storageId, out long oldTime) && time - oldTime < 500)
                {
                    // Not allow to update yet
                    return Results.BadRequest();
                }
                updatingStorages.TryRemove(storageId, out _);
                updatingStorages.TryAdd(storageId, time);
            }
            return Results.Ok(new ReadStorageItemsResp()
            {
                StorageCharacterItems = ReadStorageItems(storageId),
            });
        }

        protected async UniTask<IResult> UpdateStorageItems(UpdateStorageItemsReq request)
        {
            StorageId storageId = new StorageId(request.StorageType, request.StorageOwnerId);
            long time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (updatingStorages.TryGetValue(storageId, out long oldTime) && time - oldTime >= 500)
            {
                // Timeout
                return Results.BadRequest();
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
            return Results.Ok();
        }

        protected async UniTask<IResult> MailList(MailListReq request)
        {
            return Results.Ok(new MailListResp()
            {
                List = Database.MailList(request.UserId, request.OnlyNewMails)
            });
        }

        protected async UniTask<IResult> UpdateReadMailState(UpdateReadMailStateReq request)
        {
            long updated = Database.UpdateReadMailState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return Results.Forbid();
            }
            return Results.Ok(new UpdateReadMailStateResp()
            {
                Mail = Database.GetMail(request.MailId, request.UserId)
            });
        }

        protected async UniTask<IResult> UpdateClaimMailItemsState(UpdateClaimMailItemsStateReq request)
        {
            long updated = Database.UpdateClaimMailItemsState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return Results.Forbid();
            }
            return Results.Ok(new UpdateClaimMailItemsStateResp()
            {
                Mail = Database.GetMail(request.MailId, request.UserId)
            });
        }

        protected async UniTask<IResult> UpdateDeleteMailState(UpdateDeleteMailStateReq request)
        {
            long updated = Database.UpdateDeleteMailState(request.MailId, request.UserId);
            if (updated <= 0)
            {
                return Results.Forbid();
            }
            return Results.Ok(new UpdateDeleteMailStateResp());
        }

        protected async UniTask<IResult> SendMail(SendMailReq request)
        {
            Mail mail = request.Mail;
            if (string.IsNullOrEmpty(mail.ReceiverId))
            {
                return Results.NotFound();
            }
            long created = Database.CreateMail(mail);
            if (created <= 0)
            {
                return Results.Forbid();
            }
            return Results.Ok(new SendMailResp());
        }

        protected async UniTask<IResult> GetMail(GetMailReq request)
        {
            return Results.Ok(new GetMailResp()
            {
                Mail = Database.GetMail(request.MailId, request.UserId),
            });
        }

        protected async UniTask<IResult> GetMailNotification(GetMailNotificationReq request)
        {
            return Results.Ok(new GetMailNotificationResp()
            {
                NotificationCount = Database.GetMailNotification(request.UserId),
            });
        }

        protected async UniTask<IResult> GetIdByCharacterName(GetIdByCharacterNameReq request)
        {
            return Results.Ok(new GetIdByCharacterNameResp()
            {
                Id = Database.GetIdByCharacterName(request.CharacterName),
            });
        }

        protected async UniTask<IResult> GetUserIdByCharacterName(GetUserIdByCharacterNameReq request)
        {
            return Results.Ok(new GetUserIdByCharacterNameResp()
            {
                UserId = Database.GetUserIdByCharacterName(request.CharacterName),
            });
        }

        protected async UniTask<IResult> GetUserUnbanTime(GetUserUnbanTimeReq request)
        {
            long unbanTime = Database.GetUserUnbanTime(request.UserId);
            return Results.Ok(new GetUserUnbanTimeResp()
            {
                UnbanTime = unbanTime,
            });
        }

        protected async UniTask<IResult> SetUserUnbanTimeByCharacterName(SetUserUnbanTimeByCharacterNameReq request)
        {
            Database.SetUserUnbanTimeByCharacterName(request.CharacterName, request.UnbanTime);
            return Results.Ok();
        }

        protected async UniTask<IResult> SetCharacterUnmuteTimeByName(SetCharacterUnmuteTimeByNameReq request)
        {
            Database.SetCharacterUnmuteTimeByName(request.CharacterName, request.UnmuteTime);
            return Results.Ok();
        }

        protected async UniTask<IResult> GetSummonBuffs(GetSummonBuffsReq request)
        {
            return Results.Ok(new GetSummonBuffsResp()
            {
                SummonBuffs = Database.GetSummonBuffs(request.CharacterId),
            });
        }

        protected async UniTask<IResult> SetSummonBuffs(SetSummonBuffsReq request)
        {
            Database.SetSummonBuffs(request.CharacterId, request.SummonBuffs);
            return Results.Ok();
        }

        protected async UniTask<IResult> FindEmail(FindEmailReq request)
        {
            return Results.Ok(new FindEmailResp()
            {
                FoundAmount = FindEmail(request.Email),
            });
        }

        protected async UniTask<IResult> ValidateEmailVerification(ValidateEmailVerificationReq request)
        {
            return Results.Ok(new ValidateEmailVerificationResp()
            {
                IsPass = Database.ValidateEmailVerification(request.UserId),
            });
        }

        protected async UniTask<IResult> GetFriendRequestNotification(GetFriendRequestNotificationReq request)
        {
            return Results.Ok(new GetFriendRequestNotificationResp()
            {
                NotificationCount = Database.GetFriendRequestNotification(request.CharacterId),
            });
        }

        protected async UniTask<IResult> UpdateUserCount(UpdateUserCountReq request)
        {
            Database.UpdateUserCount(request.UserCount);
            return Results.Ok();
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
