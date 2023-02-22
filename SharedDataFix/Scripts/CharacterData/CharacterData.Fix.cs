namespace MultiplayerARPG
{
    public partial class CharacterData
    {
        public void FillWeaponSetsIfNeeded(byte equipWeaponSet)
        {
            while (SelectableWeaponSets.Count <= equipWeaponSet)
            {
                SelectableWeaponSets.Add(new EquipWeapons());
            }
        }

        public void MarkToMakeCaches()
        {
            // Do nothing
        }
    }
}
