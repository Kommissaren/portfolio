using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SharpDungeonCrawler
{
    class Program
    {
        static Random rng = new Random();
        static int playerGold = 50;
        static Dictionary<string, int> areaEnemiesDefeated = new Dictionary<string, int>();
        static HashSet<string> clearedAreas = new HashSet<string>();
        static HashSet<string> unlockedDerivatives = new HashSet<string>();

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== Welcome to Sharp Dungeon Crawler ===\n");
            Console.ResetColor();

            Hero player = CreateHero();
            InitializeAreas();

            while (true)
            {
                MainMenu(player);
            }
        }

        static Hero CreateHero()
        {
            Console.WriteLine("Choose your class:");
            Console.WriteLine("1. Rogue (High Crit & Dodge, Low HP)");
            Console.WriteLine("2. Mage (Skills-based, Low HP)");
            Console.WriteLine("3. Paladin (Healing, High HP, Low DMG)");
            Console.WriteLine("4. Barbarian (High DMG, Okay HP, No healing)");
            Console.WriteLine("5. Druid (Summon Companion, DoT, Moderate HP)");
            Console.WriteLine("6. Ranger (High Crit & Crit DMG, Moderate HP, Dodge)");

            int choice = GetMenuChoice(1, 6);
            Console.WriteLine("Enter your hero's name:");
            string name = Console.ReadLine() ?? "Hero";

            switch (choice)
            {
                case 1:
                    return new Hero(name, "Rogue", 70, 10, 25, 20, new List<Skill>(), 1);
                case 2:
                    return new Hero(name, "Mage", 60, 8, 10, 5, new List<Skill> { new Skill("Fireball", SkillType.Attack, 15, 0, DamageType.Fire, "Magic attack") }, 1);
                case 3:
                    return new Hero(name, "Paladin", 100, 8, 5, 5, new List<Skill> { new Skill("Heal", SkillType.Heal, 0, 25, DamageType.Radiant, "Heals HP") }, 1);
                case 4:
                    return new Hero(name, "Barbarian", 90, 15, 5, 0, new List<Skill>(), 1);
                case 5:
                    return new Hero(name, "Druid", 80, 10, 10, 5, new List<Skill> { new Skill("Summon Companion", SkillType.Companion, 20, 0, DamageType.Poison, "Deals double damage on strike") }, 1);
                case 6:
                    return new Hero(name, "Ranger", 80, 12, 20, 15, new List<Skill> { new Skill("Arrow Volley", SkillType.Attack, 20, 0, DamageType.Slash, "High crit chance AoE attack") }, 1);
                default:
                    return new Hero(name, "Rogue", 70, 10, 25, 20, new List<Skill>(), 1);
            }
        }

        static void InitializeAreas()
        {
            string[] areas = { "Forest", "Cave", "Mountain", "Mistlands", "Swamplands", "Ruined Fortress", "Lava Pits" };
            foreach (var a in areas) areaEnemiesDefeated[a] = 0;
        }

        static void MainMenu(Hero player)
        {
            Console.WriteLine("\n=== Main Menu ===");
            Console.WriteLine("1. Explore Area");
            Console.WriteLine("2. View Stats");
            Console.WriteLine("3. Visit Tavern");
            Console.WriteLine("4. Class Unlock Menu");
            Console.WriteLine("5. Exit Game");

            int choice = GetMenuChoice(1, 5);
            switch (choice)
            {
                case 1: ExploreArea(player); break;
                case 2: player.DisplayStats(); break;
                case 3: Tavern(player); break;
                case 4: ClassUnlockMenu(player); break;
                case 5:
                    Console.WriteLine("Exiting game... Goodbye!");
                    Environment.Exit(0);
                    break;
            }
        }

        static void ExploreArea(Hero player)
        {
            Console.WriteLine("\nChoose an area to explore:");
            int idx = 1;
            foreach (var area in areaEnemiesDefeated.Keys)
            {
                if (!clearedAreas.Contains(area)) Console.WriteLine($"{idx++}. {area}");
            }
            if (clearedAreas.Count >= 7)
            {
                Console.WriteLine($"{idx}. Boss Lair");
            }
            Console.WriteLine($"{idx + 1}. Cancel");

            int choice = GetMenuChoice(1, idx + 1);
            int areaCount = 0;
            foreach (var area in areaEnemiesDefeated.Keys)
            {
                if (!clearedAreas.Contains(area)) areaCount++;
                if (choice == areaCount)
                {
                    EnterArea(player, area);
                    return;
                }
            }

            if (clearedAreas.Count >= 7 && choice == idx) BossLair(player);
        }

        static void EnterArea(Hero player, string area)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n--- Entering {area} ---\n");
            Console.ResetColor();

            int enemiesToDefeat = 15;
            while (areaEnemiesDefeated[area] < enemiesToDefeat && player.Health > 0)
            {
                RandomSideEvent(player, area);
                Enemy enemy = GenerateEnemy(area, player.Level);
                Battle(player, new List<Enemy> { enemy });

                areaEnemiesDefeated[area]++;
            }

            if (player.Health > 0)
            {
                Enemy midBoss = GenerateMidBoss(area, player.Level);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nMid-Boss Appears! {midBoss.Name} HP: {midBoss.Health}\n");
                Console.ResetColor();
                Battle(player, new List<Enemy> { midBoss });

                clearedAreas.Add(area);
                player.Health += 25 + player.Level;
                player.BaseDamage += 4;
                Console.WriteLine($"\nArea Cleared! +{25 + player.Level} HP, +4 DMG\n");
            }
        }

        static void RandomSideEvent(Hero player, string area)
        {
            int chance = rng.Next(0, 100);

            if (chance < 20)
            {
                Console.WriteLine("\nYou find a hidden stash! +10-20 Gold");
                playerGold += rng.Next(10, 21);
            }
            else if (chance < 35)
            {
                int hp = rng.Next(5, 16);
                Console.WriteLine($"\nYou step on a healing herb! +{hp} HP");
                player.Health += hp;
            }
            else if (chance < 50)
            {
                int dmg = rng.Next(5, 16);
                Console.WriteLine($"\nA trap springs! -{dmg} HP");
                player.Health -= dmg;
            }
            else if (chance < 70)
            {
                Console.WriteLine("\nYou meet a wandering merchant!");
                Console.WriteLine("Sidequest: Recover his lost gem. Reward: Gold + XP");
                int choice = rng.Next(1, 3);
                if (choice == 1)
                {
                    Console.WriteLine("You return the gem successfully! +30 Gold, +10 XP");
                    playerGold += 30;
                    player.GainExperience(10);
                }
                else
                {
                    Console.WriteLine("You fail to find the gem. No reward.");
                }
            }
        }

        static Enemy GenerateEnemy(string area, int playerLevel)
        {
            List<Enemy> possible = new List<Enemy>();
            switch (area)
            {
                case "Forest":
                    possible.Add(new Enemy("Wolf", 30 + playerLevel * 2, 5, 10, DamageType.Slash));
                    possible.Add(new Enemy("Goblin", 25 + playerLevel * 2, 4, 8, DamageType.Blunt));
                    possible.Add(new Enemy("Treant", 40 + playerLevel * 3, 6, 12, DamageType.Slash));
                    possible.Add(new Enemy("Bandit Archer", 20 + playerLevel * 2, 5, 10, DamageType.Slash));
                    possible.Add(new Enemy("Poisonous Snake", 15 + playerLevel * 2, 4, 8, DamageType.Poison));
                    break;
                case "Cave":
                    possible.Add(new Enemy("Bat", 20 + playerLevel * 2, 4, 7, DamageType.Slash));
                    possible.Add(new Enemy("Cave Spider", 25 + playerLevel * 2, 5, 9, DamageType.Poison));
                    possible.Add(new Enemy("Goblin Miner", 30 + playerLevel * 2, 6, 12, DamageType.Blunt));
                    possible.Add(new Enemy("Stone Golem", 40 + playerLevel * 3, 7, 14, DamageType.Blunt));
                    possible.Add(new Enemy("Shadow Lurker", 35 + playerLevel * 2, 6, 13, DamageType.Slash));
                    break;
                // Other areas here...
                default:
                    possible.Add(new Enemy("Weak Enemy", 20, 5, 10, DamageType.Slash));
                    break;
            }

            return possible[rng.Next(possible.Count)];
        }

        static Enemy GenerateMidBoss(string area, int playerLevel)
        {
            switch (area)
            {
                case "Forest": return new Enemy("Forest Guardian", 250 + playerLevel * 5, 12, 20, DamageType.Slash);
                case "Cave": return new Enemy("Cave Troll", 250 + playerLevel * 5, 12, 22, DamageType.Blunt);
                case "Mountain": return new Enemy("Mountain Drake", 250 + playerLevel * 5, 15, 25, DamageType.Fire);
                default: return new Enemy("Generic MidBoss", 250, 12, 20, DamageType.Slash);
            }
        }

        static void Battle(Hero player, List<Enemy> enemies)
        {
            while (player.Health > 0 && enemies.Any(e => e.Health > 0))
            {
                Console.WriteLine($"\nYour HP: {player.Health} | Gold: {playerGold}");
                for (int i = 0; i < enemies.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {enemies[i].Name} HP: {enemies[i].Health}/{enemies[i].MaxHealth}");
                }

                Console.WriteLine("\nChoose action:");
                Console.WriteLine("1. Basic Attack");
                Console.WriteLine("2. Use Skill");
                Console.WriteLine("3. Heal");

                int choice = GetMenuChoice(1, 3);
                switch (choice)
                {
                    case 1:
                        BasicAttack(player, enemies[rng.Next(enemies.Count)]);
                        break;
                    case 2:
                        if (player.Skills.Count == 0)
                        {
                            Console.WriteLine("No skills available!");
                        }
                        else
                        {
                            Console.WriteLine("Choose skill:");
                            for (int i = 0; i < player.Skills.Count; i++)
                                Console.WriteLine($"{i + 1}. {player.Skills[i].Name}: {player.Skills[i].Description}");
                            int skillChoice = GetMenuChoice(1, player.Skills.Count);
                            UseSkill(player, enemies[rng.Next(enemies.Count)], player.Skills[skillChoice - 1]);
                        }
                        break;
                    case 3:
                        int healAmount = 20 + player.Level;
                        Console.WriteLine($"\nYou heal {healAmount} HP!");
                        player.Health += healAmount;
                        break;
                }

                foreach (var enemy in enemies.Where(e => e.Health > 0))
                {
                    int dmg = rng.Next(enemy.MinDamage, enemy.MaxDamage + 1);
                    Console.WriteLine($"{enemy.Name} attacks you for {dmg} damage!");
                    player.Health -= dmg;
                }
            }

            if (player.Health > 0)
            {
                int xpGain = enemies.Count * 10;
                Console.WriteLine($"\nBattle won! +{xpGain} XP");
                player.GainExperience(xpGain);
                playerGold += rng.Next(10, 31);
            }
            else
            {
                Console.WriteLine("\nYou have been defeated! Resting and returning to town...");
                player.Health = 50 + player.Level * 5;
            }
        }

        static void BasicAttack(Hero player, Enemy enemy)
        {
            int critRoll = rng.Next(0, 100);
            int damage = player.BaseDamage;
            if (critRoll < player.CritChance)
            {
                damage *= 2;
                Console.WriteLine("Critical Hit!");
            }
            enemy.Health -= damage;
            Console.WriteLine($"You hit {enemy.Name} for {damage} damage! HP left: {enemy.Health}/{enemy.MaxHealth}");
        }

        static void UseSkill(Hero player, Enemy enemy, Skill skill)
        {
            switch (skill.Type)
            {
                case SkillType.Attack:
                    enemy.Health -= skill.Damage;
                    Console.WriteLine($"Used {skill.Name} on {enemy.Name}, dealing {skill.Damage} damage!");
                    break;
                case SkillType.Heal:
                    player.Health += skill.HealAmount;
                    Console.WriteLine($"Used {skill.Name}, healing {skill.HealAmount} HP!");
                    break;
                case SkillType.Companion:
                    int compDmg = skill.Damage * 2;
                    enemy.Health -= compDmg;
                    Console.WriteLine($"Companion strikes! {enemy.Name} takes {compDmg} damage!");
                    break;
                case SkillType.DamageOverTime:
                    int dotDmg = skill.Damage;
                    enemy.Health -= dotDmg;
                    Console.WriteLine($"{enemy.Name} suffers {dotDmg} DoT from {skill.Name}!");
                    break;
            }
        }

        static void Tavern(Hero player)
        {
            Console.WriteLine("\n=== Tavern ===\n");
            Console.WriteLine("1. Trade Gold for Gear");
            Console.WriteLine("2. Experience Random Event");
            Console.WriteLine("3. Leave Tavern");

            int choice = GetMenuChoice(1, 3);
            switch (choice)
            {
                case 1: TradeGear(player); break;
                case 2: RandomTavernEvent(player); break;
                case 3: Console.WriteLine("Leaving Tavern..."); break;
            }
        }

        static void TradeGear(Hero player)
        {
            List<Gear> shopItems = new List<Gear>
            {
                new Gear("Leather Armor", 20, 2, 25),
                new Gear("Steel Sword", 0, 5, 50),
                new Gear("Mithril Armor", 50, 10, 100),
                new Gear("Mithril Sword", 0, 15, 120)
            };

            Console.WriteLine("\n--- Gear Shop ---");
            for (int i = 0; i < shopItems.Count; i++)
                Console.WriteLine($"{i + 1}. {shopItems[i].Name} | +{shopItems[i].HPBonus} HP, +{shopItems[i].DamageBonus} DMG | Cost: {shopItems[i].Cost} Gold");

            Console.WriteLine($"{shopItems.Count + 1}. Cancel");
            int choice = GetMenuChoice(1, shopItems.Count + 1);
            if (choice == shopItems.Count + 1) return;

            Gear selected = shopItems[choice - 1];
            if (playerGold >= selected.Cost)
            {
                playerGold -= selected.Cost;
                player.Health += selected.HPBonus;
                player.BaseDamage += selected.DamageBonus;
                player.Inventory.Add(selected.Name);
                Console.WriteLine($"\nPurchased {selected.Name}! +{selected.HPBonus} HP, +{selected.DamageBonus} DMG");
            }
            else Console.WriteLine("Not enough gold!");
        }

        static void RandomTavernEvent(Hero player)
        {
            int chance = rng.Next(0, 100);
            if (chance < 30)
            {
                int goldFound = rng.Next(10, 31);
                Console.WriteLine($"\nFound hidden coin pouch! +{goldFound} Gold");
                playerGold += goldFound;
            }
            else if (chance < 50)
            {
                int hp = rng.Next(10, 26);
                Console.WriteLine($"\nMagical spring heals +{hp} HP");
                player.Health += hp;
            }
            else if (chance < 70)
            {
                List<Gear> loot = new List<Gear>
                {
                    new Gear("Rusty Dagger",0,3,0),
                    new Gear("Old Shield",15,0,0),
                    new Gear("Potion of Strength",0,5,0)
                };
                Gear g = loot[rng.Next(loot.Count)];
                Console.WriteLine($"\nFound loot: {g.Name} (+{g.HPBonus} HP, +{g.DamageBonus} DMG)");
                player.Health += g.HPBonus;
                player.BaseDamage += g.DamageBonus;
                player.Inventory.Add(g.Name);
            }
            else
            {
                int dmg = rng.Next(5, 16);
                Console.WriteLine($"\nRowdy patron bumps you! -{dmg} HP");
                player.Health -= dmg;
            }
        }

        static void ClassUnlockMenu(Hero player)
        {
            Console.WriteLine("\n=== Class Unlock Menu ===");
            Console.WriteLine("Unlocked Derivatives:");
            if (unlockedDerivatives.Count == 0) Console.WriteLine("None yet.");
            else foreach (var d in unlockedDerivatives) Console.WriteLine($"- {d}");

            Console.WriteLine("\nEnter phrase to unlock (or EXIT to return):");
            string? phrase = Console.ReadLine()?.Trim().ToUpper();
            if (string.IsNullOrEmpty(phrase) || phrase == "EXIT") return;

            // Example unlocks
            if (player.ClassName == "Paladin" && phrase == "BANKAI" && !unlockedDerivatives.Contains("Templar"))
            {
                unlockedDerivatives.Add("Templar");
                player.ClassName = "Templar";
                player.Skills.Add(new Skill("Radiant Strike", SkillType.Attack, 35, 0, DamageType.Radiant, "Holy damage"));
                Console.WriteLine("Templar unlocked!");
            }
            else Console.WriteLine("Invalid phrase or already unlocked.");
        }

        static void BossLair(Hero player)
        {
            Enemy finalBoss = new Enemy("Final Boss", 1000, 20, 35, DamageType.Slash);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n=== Final Boss Lair ===");
            Console.ResetColor();
            Battle(player, new List<Enemy> { finalBoss });

            if (player.Health > 0)
            {
                Console.WriteLine("Congratulations! Final Boss defeated! Dungeon resets.");
                clearedAreas.Clear();
                InitializeAreas();
            }
        }

        static int GetMenuChoice(int min, int max)
        {
            int choice;
            while (!int.TryParse(Console.ReadLine(), out choice) || choice < min || choice > max)
                Console.WriteLine($"Enter a number between {min} and {max}:");
            return choice;
        }

        public class Hero
        {
            public string Name, ClassName;
            public int Health, BaseDamage, CritChance, DodgeChance;
            public int Level;
            public List<Skill> Skills;
            public List<string> Inventory;

            public Hero(string name, string className, int hp, int dmg, int crit, int dodge, List<Skill> skills, int level)
            {
                Name = name;
                ClassName = className;
                Health = hp;
                BaseDamage = dmg;
                CritChance = crit;
                DodgeChance = dodge;
                Skills = skills;
                Level = level;
                Inventory = new List<string>();
            }

            public void DisplayStats()
            {
                Console.WriteLine($"\nName: {Name} | Class: {ClassName} | HP: {Health} | DMG: {BaseDamage} | Crit: {CritChance}% | Dodge: {DodgeChance}% | Level: {Level}");
                if (Skills.Count > 0)
                {
                    Console.WriteLine("Skills:");
                    foreach (var s in Skills) Console.WriteLine($"- {s.Name}: {s.Description}");
                }
                if (Inventory.Count > 0) Console.WriteLine("Inventory: " + string.Join(", ", Inventory));
            }

            public void GainExperience(int amount)
            {
                Level++;
                Health += 10;
                BaseDamage += 2;
                Console.WriteLine($"\nLevel Up! Level {Level}, +10 HP, +2 DMG");
            }
        }

        public class Enemy
        {
            public string Name;
            public int Health, MaxHealth, MinDamage, MaxDamage;
            public DamageType DamageType;
            public Enemy(string name, int hp, int minDmg, int maxDmg, DamageType type)
            {
                Name = name; Health = hp; MaxHealth = hp; MinDamage = minDmg; MaxDamage = maxDmg; DamageType = type;
            }
        }

        public class Skill
        {
            public string Name;
            public SkillType Type;
            public int Damage;
            public int HealAmount;
            public DamageType DamageType;
            public string Description;

            public Skill(string name, SkillType type, int dmg, int heal, DamageType dmgType, string desc)
            {
                Name = name; Type = type; Damage = dmg; HealAmount = heal; DamageType = dmgType; Description = desc;
            }
        }

        public class Gear
        {
            public string Name;
            public int HPBonus;
            public int DamageBonus;
            public int Cost;
            public Gear(string name, int hp, int dmg, int cost) { Name = name; HPBonus = hp; DamageBonus = dmg; Cost = cost; }
        }

        public enum DamageType { Magic, Fire, Poison, Radiant, Slash, Blunt }
        public enum SkillType { Attack, Heal, Buff, Companion, DamageOverTime }
    }
}
