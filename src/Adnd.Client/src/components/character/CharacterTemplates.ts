// Background templates
export interface BackgroundTemplate {
  id: string;
  name: string;
  description: string;
  skillBonuses: string[];
  feature: string;
  featureDescription: string;
  languages: string[];
  startingEquipment: { name: string; type: string; quantity: number }[];
}

export const BACKGROUND_TEMPLATES: BackgroundTemplate[] = [
  { id: 'acolyte', name: 'Acolyte', description: 'You have spent your life in the service of a temple to a specific god or pantheon.', skillBonuses: ['Insight', 'Religion'], feature: 'Shelter of the Faithful', featureDescription: 'You and your companions can rest and receive healing at temples of your faith.', languages: [], startingEquipment: [{ name: 'Holy symbol', type: 'Tool', quantity: 1 }, { name: 'Prayer book', type: 'Tool', quantity: 1 }, { name: 'Incense', type: 'Equipment', quantity: 5 }, { name: 'Vestments', type: 'Equipment', quantity: 1 }, { name: '5 sticks of incense', type: 'Equipment', quantity: 5 }] },
  { id: 'criminal', name: 'Criminal', description: 'You were once deeply involved in a life of crime.', skillBonuses: ['Deception', 'Stealth'], feature: 'Dark Web', featureDescription: 'You still have contacts in the criminal underworld.', languages: [], startingEquipment: [{ name: 'Crowbar', type: 'Tool', quantity: 1 }, { name: 'Dark common clothes', type: 'Clothing', quantity: 1 }, { name: 'Mask', type: 'Clothing', quantity: 1 }, { name: '15 gold pieces', type: 'Currency', quantity: 15 }] },
  { id: 'soldier', name: 'Soldier', description: 'Life has been hard and unforgiving, but you have always had the unwavering resolve to face your problems.', skillBonuses: ['Athletics', 'Intimidation'], feature: 'Military Rank', featureDescription: 'You have a military rank from your time as a soldier.', languages: [], startingEquipment: [{ name: 'Insignia of rank', type: 'Tool', quantity: 1 }, { name: 'Trophy from fallen enemy', type: 'Item', quantity: 1 }, { name: 'Bone dice', type: 'Game Set', quantity: 1 }, { name: 'Cloak', type: 'Clothing', quantity: 1 }] },
  { id: 'sage', name: 'Sage', description: 'You spent years learning the lore of the multiverse.', skillBonuses: ['Arcana', 'History'], feature: 'Researcher', featureDescription: 'You can access rare books and scholarly contacts.', languages: [], startingEquipment: [{ name: 'Bottle of ink', type: 'Writing Material', quantity: 1 }, { name: 'Ink pen', type: 'Writing Material', quantity: 1 }, { name: 'Letter of finding from mentor', type: 'Document', quantity: 1 }, { name: 'Common clothes', type: 'Clothing', quantity: 1 }] },
  { id: 'gladiator', name: 'Gladiator', description: 'You fought in arenas for the entertainment of crowds.', skillBonuses: ['Athletics', 'Performance'], feature: 'Arena Fame', featureDescription: 'You are known in certain circles.', languages: [], startingEquipment: [{ name: 'Weapon of your class', type: 'Weapon', quantity: 1 }, { name: 'Armor of your class', type: 'Armor', quantity: 1 }, { name: 'Trophy from a defeated foe', type: 'Item', quantity: 1 }, { name: 'Common clothes', type: 'Clothing', quantity: 1 }] },
  { id: 'folk-hero', name: 'Folk Hero', description: 'You come from a humble social origin, but have achieved greatness.', skillBonuses: ['Athletics', 'Animal Handling'], feature: 'Country Blessing', featureDescription: 'You may receive free healing and shelter from common folk.', languages: [], startingEquipment: [{ name: 'Shovel', type: 'Tool', quantity: 1 }, { name: 'Iron pot', type: 'Tool', quantity: 1 }, { name: 'Common clothes', type: 'Clothing', quantity: 1 }, { name: 'Belt pouch with 10 gp', type: 'Currency', quantity: 10 }] },
  { id: 'urchin', name: 'Urchin', description: 'You grew up on the streets alone, orphaned at an early age.', skillBonuses: ['Sleight of Hand', 'Stealth'], feature: 'City Secrets', featureDescription: 'You know the hidden paths and secrets of the city.', languages: [], startingEquipment: [{ name: 'Small knife', type: 'Weapon', quantity: 1 }, { name: 'Map of the city', type: 'Map', quantity: 1 }, { name: 'Dog token', type: 'Trinket', quantity: 1 }, { name: 'Rat dog companion', type: 'Companion', quantity: 1 }] },
  { id: 'noble', name: 'Noble', description: 'You understand wealth, power, and privilege.', skillBonuses: ['History', 'Persuasion'], feature: 'Position of Privilege', featureDescription: 'You can invoke your noble status to gain audience with nobles.', languages: [], startingEquipment: [{ name: 'Signet ring', type: 'Trinket', quantity: 1 }, { name: 'Scroll of pedigree', type: 'Document', quantity: 1 }, { name: 'Fine clothes', type: 'Clothing', quantity: 1 }, { name: 'Pouch with 25 gp', type: 'Currency', quantity: 25 }] },
];

export interface ClassTemplate {
  id: string;
  name: string;
  description: string;
  hitDie: number;
  primaryAttributes: string[];
  defaultAttributes: Record<string, number>;
}

export const CLASS_TEMPLATES: ClassTemplate[] = [
  { id: 'barbarian', name: 'Barbarian', description: 'A fierce warrior who can enter a battle rage', hitDie: 12, primaryAttributes: ['STR'], defaultAttributes: { STR: 15, DEX: 12, CON: 14, INT: 8, WIS: 10, CHA: 8 } },
  { id: 'bard', name: 'Bard', description: 'A master of music and magic', hitDie: 8, primaryAttributes: ['CHA'], defaultAttributes: { STR: 8, DEX: 14, CON: 12, INT: 10, WIS: 10, CHA: 15 } },
  { id: 'cleric', name: 'Cleric', description: 'A priestly champion who wields divine magic', hitDie: 8, primaryAttributes: ['WIS'], defaultAttributes: { STR: 14, DEX: 10, CON: 12, INT: 10, WIS: 16, CHA: 8 } },
  { id: 'druid', name: 'Druid', description: 'A priest of the Old Faith, wielding the power of the forests', hitDie: 8, primaryAttributes: ['WIS'], defaultAttributes: { STR: 10, DEX: 14, CON: 12, INT: 12, WIS: 16, CHA: 8 } },
  { id: 'fighter', name: 'Fighter', description: 'A master of martial combat', hitDie: 10, primaryAttributes: ['STR', 'DEX'], defaultAttributes: { STR: 16, DEX: 14, CON: 14, INT: 10, WIS: 10, CHA: 8 } },
  { id: 'monk', name: 'Monk', description: 'A master of martial arts', hitDie: 8, primaryAttributes: ['DEX', 'WIS'], defaultAttributes: { STR: 10, DEX: 16, CON: 14, INT: 10, WIS: 14, CHA: 8 } },
  { id: 'paladin', name: 'Paladin', description: 'A holy warrior bound to a sacred oath', hitDie: 10, primaryAttributes: ['STR', 'CHA'], defaultAttributes: { STR: 16, DEX: 10, CON: 14, INT: 10, WIS: 10, CHA: 14 } },
  { id: 'ranger', name: 'Ranger', description: 'A warrior who uses martial prowess and nature magic', hitDie: 10, primaryAttributes: ['DEX', 'WIS'], defaultAttributes: { STR: 12, DEX: 16, CON: 14, INT: 10, WIS: 14, CHA: 8 } },
  { id: 'rogue', name: 'Rogue', description: 'A scoundrel who uses stealth and guile', hitDie: 8, primaryAttributes: ['DEX'], defaultAttributes: { STR: 10, DEX: 16, CON: 12, INT: 14, WIS: 10, CHA: 12 } },
  { id: 'sorcerer', name: 'Sorcerer', description: 'A spellcaster who draws on inherent magic', hitDie: 6, primaryAttributes: ['CHA'], defaultAttributes: { STR: 8, DEX: 14, CON: 12, INT: 10, WIS: 10, CHA: 16 } },
  { id: 'warlock', name: 'Warlock', description: 'A wizard who has made a pact with a powerful being', hitDie: 8, primaryAttributes: ['CHA'], defaultAttributes: { STR: 10, DEX: 14, CON: 12, INT: 10, WIS: 10, CHA: 16 } },
  { id: 'wizard', name: 'Wizard', description: 'A spellcaster who draws on the fabric of magic', hitDie: 6, primaryAttributes: ['INT'], defaultAttributes: { STR: 8, DEX: 14, CON: 12, INT: 16, WIS: 10, CHA: 10 } },
];

export const STANDARD_ARRAYS: number[][] = [
  [15, 14, 13, 12, 10, 8], [15, 14, 12, 12, 10, 8], [15, 13, 12, 12, 10, 8],
  [14, 14, 12, 12, 10, 10], [14, 13, 13, 12, 12, 8], [13, 13, 12, 12, 12, 10],
];

export const RACES = [
  { id: 'human', name: 'Human', abilityScoreBonuses: { STR: 1, DEX: 1, CON: 1, INT: 1, WIS: 1, CHA: 1 }, speed: 30, traits: ['Extra Language'] },
  { id: 'high-elf', name: 'High Elf', abilityScoreBonuses: { DEX: 2, INT: 1 }, speed: 30, traits: ['Darkvision', 'Fey Ancestry', 'Trance', 'Cantrip'] },
  { id: 'wood-elf', name: 'Wood Elf', abilityScoreBonuses: { DEX: 2, WIS: 1 }, speed: 35, traits: ['Darkvision', 'Fey Ancestry', 'Trance', 'Fleet of Foot'] },
  { id: 'dwarf', name: 'Dwarf', abilityScoreBonuses: { CON: 2 }, speed: 25, traits: ['Darkvision', 'Dwarven Resilience', 'Tool Proficiency'] },
  { id: 'halfling', name: 'Halfling', abilityScoreBonuses: { DEX: 2 }, speed: 25, traits: ['Lucky', 'Brave', 'Halfling Nimbleness'] },
  { id: 'dragonborn', name: 'Dragonborn', abilityScoreBonuses: { STR: 2, CHA: 1 }, speed: 30, traits: ['Draconic Ancestry', 'Breath Weapon'] },
  { id: 'tiefling', name: 'Tiefling', abilityScoreBonuses: { CHA: 2, INT: 1 }, speed: 30, traits: ['Darkvision', 'Hellish Resistance'] },
  { id: 'half-elf', name: 'Half-Elf', abilityScoreBonuses: {}, speed: 30, traits: ['Darkvision', 'Fey Ancestry', 'Skill Versatility'] },
  { id: 'half-orc', name: 'Half-Orc', abilityScoreBonuses: { STR: 2, CON: 1 }, speed: 30, traits: ['Darkvision', 'Relentless Endurance'] },
];

export const LANGUAGES = ['Common', 'Elvish', 'Dwarvish', 'Gnomish', 'Goblin', 'Draconic', 'Infernal', 'Primordial', 'Sylvan', 'Undercommon'];
