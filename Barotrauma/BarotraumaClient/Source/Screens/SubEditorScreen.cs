﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Barotrauma
{
    class SubEditorScreen : Screen
    {
        private static string[] crewExperienceLevels = new string[] 
        {
            TextManager.Get("CrewExperienceLow"),
            TextManager.Get("CrewExperienceMid"),
            TextManager.Get("CrewExperienceHigh")
        };


        private Camera cam;
        private BlurEffect lightBlur;

        private bool lightingEnabled;

        public GUIComponent topPanel, leftPanel;

        private bool entityMenuOpen;
        private GUIComponent entityMenu;
        private GUITextBox entityFilterBox;
        private GUIListBox entityList;

        private GUIFrame loadFrame, saveFrame;

        private GUITextBox nameBox;

        private GUIFrame hullVolumeFrame;

        private GUIFrame saveAssemblyFrame;

        const int PreviouslyUsedCount = 10;
        private GUIListBox previouslyUsedList;

        private GUIDropDown linkedSubBox;

        //a Character used for picking up and manipulating items
        private Character dummyCharacter;
        
        private bool characterMode;

        private bool wiringMode;
        private GUIFrame wiringToolPanel;

        private Tutorials.EditorTutorial tutorial;

        public override Camera Cam
        {
            get { return cam; }
        }
        
        public string GetSubName()
        {
            return (Submarine.MainSub == null) ? "" : Submarine.MainSub.Name;
        }

        private string GetItemCount()
        {
            return TextManager.Get("Items") + ": " + Item.ItemList.Count;
        }

        private string GetStructureCount()
        {
            return TextManager.Get("Structures") + ": " + (MapEntity.mapEntityList.Count - Item.ItemList.Count);
        }

        private string GetTotalHullVolume()
        {
            return TextManager.Get("TotalHullVolume") + ":\n" + Hull.hullList.Sum(h => h.Volume);
        }

        private string GetSelectedHullVolume()
        {
            float buoyancyVol = 0.0f;
            float selectedVol = 0.0f;
            float neutralPercentage = 0.07f;
            Hull.hullList.ForEach(h =>
            {
                buoyancyVol += h.Volume;
                if (h.IsSelected)
                {
                    selectedVol += h.Volume;
                }
            });
            buoyancyVol *= neutralPercentage;
            string retVal = TextManager.Get("SelectedHullVolume") + ":\n" + selectedVol;
            if (selectedVol > 0.0f && buoyancyVol > 0.0f)
            {
                if (buoyancyVol / selectedVol < 1.0f)
                {
                    retVal += " (" + TextManager.Get("OptimalBallastLevel").Replace("[value]", (buoyancyVol / selectedVol).ToString("0.00")) + ")";
                }
                else
                {
                    retVal += " (" + TextManager.Get("InsufficientBallast") + ")";
                }
            }
            return retVal;
        }

        private string GetPhysicsBodyCount()
        {
            return TextManager.Get("PhysicsBodies") + ": " + GameMain.World.BodyList.Count;
        }

        public bool CharacterMode
        {
            get { return characterMode; }
        }

        public bool WiringMode
        {
            get { return wiringMode; }
        }


        public SubEditorScreen(ContentManager content)
        {
            cam = new Camera();
#if LINUX || OSX
            var blurEffect = content.Load<Effect>("Effects/blurshader_opengl");
#else
            var blurEffect = content.Load<Effect>("Effects/blurshader");
#endif
            lightBlur = new BlurEffect(blurEffect, 0.001f, 0.001f);
            
            topPanel = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.04f), GUI.Canvas) { MinSize = new Point(0, 35) }, "GUIFrameTop");
            GUIFrame paddedTopPanel = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.55f), topPanel.RectTransform, Anchor.Center) { RelativeOffset = new Vector2(0.0f, -0.1f) }, 
                style: null);

            hullVolumeFrame = new GUIFrame(new RectTransform(new Vector2(0.15f, 0.8f), topPanel.RectTransform, Anchor.BottomLeft, Pivot.TopLeft) { RelativeOffset = new Vector2(0.08f, 0.0f) }, "InnerFrame")
            {
                Visible = false
            };

            GUITextBlock totalHullVolume = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), hullVolumeFrame.RectTransform), "", font: GUI.SmallFont)
            {
                TextGetter = GetTotalHullVolume
            };

            GUITextBlock selectedHullVolume = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), hullVolumeFrame.RectTransform) { RelativeOffset = new Vector2(0.0f, 0.15f) }, "", font: GUI.SmallFont)
            {
                TextGetter = GetSelectedHullVolume
            };

            saveAssemblyFrame = new GUIFrame(new RectTransform(new Vector2(0.08f, 0.03f), topPanel.RectTransform, Anchor.BottomRight, Pivot.TopRight), "InnerFrame")
            {
                Visible = false
            };

            var saveAssemblyButton = new GUIButton(new RectTransform(new Vector2(0.9f, 0.8f), saveAssemblyFrame.RectTransform, Anchor.Center), TextManager.Get("SaveItemAssembly"));
            saveAssemblyFrame.Font = GUI.SmallFont;
            saveAssemblyButton.OnClicked += (btn, userdata) =>
            {
                CreateSaveAssemblyScreen();
                return true;
            };

            var button = new GUIButton(new RectTransform(new Vector2(0.07f, 0.9f), paddedTopPanel.RectTransform, Anchor.CenterLeft), TextManager.Get("OpenSubButton"));
            button.OnClicked = (GUIButton btn, object data) =>
            {
                saveFrame = null;
                entityMenuOpen = false;
                CreateLoadScreen();

                return true;
            };

            button = new GUIButton(new RectTransform(new Vector2(0.07f, 0.9f), paddedTopPanel.RectTransform, Anchor.CenterLeft) { RelativeOffset = new Vector2(0.08f, 0.0f) }, TextManager.Get("SaveSubButton"));
            button.OnClicked = (GUIButton btn, object data) =>
            {
                loadFrame = null;
                entityMenuOpen = false;
                CreateSaveScreen();

                return true;
            };

            var nameLabel = new GUITextBlock(new RectTransform(new Vector2(0.1f, 0.9f), paddedTopPanel.RectTransform, Anchor.CenterLeft) { RelativeOffset = new Vector2(0.15f, 0.0f) },
                "", font: GUI.LargeFont, textAlignment: Alignment.CenterLeft)
            {
                TextGetter = GetSubName
            };

            linkedSubBox = new GUIDropDown(new RectTransform(new Vector2(0.1f, 0.9f), paddedTopPanel.RectTransform) { RelativeOffset = new Vector2(0.4f, 0.0f) },
                TextManager.Get("AddSubButton"))
            {
                ToolTip = TextManager.Get("AddSubToolTip")
            };

            foreach (Submarine sub in Submarine.SavedSubmarines)
            {
                linkedSubBox.AddItem(sub.Name, sub);
            }
            linkedSubBox.OnSelected += SelectLinkedSub;
            linkedSubBox.OnDropped += (component, obj) =>
            {
                MapEntity.SelectedList.Clear();
                return true;
            };

            leftPanel = new GUIFrame(new RectTransform(new Vector2(0.08f, 1.0f), GUI.Canvas) { MinSize = new Point(130, 0) }, "GUIFrameLeft");
            GUILayoutGroup paddedLeftPanel = new GUILayoutGroup(new RectTransform(
                new Point((int)(leftPanel.Rect.Width * 0.8f), (int)(GameMain.GraphicsHeight - topPanel.Rect.Height * 0.95f)),
                leftPanel.RectTransform, Anchor.Center) { AbsoluteOffset = new Point(0, topPanel.Rect.Height) }, 
                spacing: 5);

            GUITextBlock itemCount = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), paddedLeftPanel.RectTransform), "ItemCount")
            {
                TextGetter = GetItemCount
            };

            GUITextBlock structureCount = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), paddedLeftPanel.RectTransform), "StructureCount")
            {
                TextGetter = GetStructureCount
            };


            foreach (MapEntityCategory category in Enum.GetValues(typeof(MapEntityCategory)))
            {
                var catButton = new GUIButton(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), category.ToString())
                {
                    UserData = category,
                    OnClicked = (btn, userdata) => { entityMenuOpen = true; OpenEntityMenu((MapEntityCategory)userdata); return true; }
                };
            }

            //Entity menu
            //------------------------------------------------

            entityMenu = new GUIFrame(new RectTransform(new Vector2(0.25f, 0.4f), GUI.Canvas, Anchor.Center) { MinSize = new Point(400, 400) });
            var paddedTab = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), entityMenu.RectTransform, Anchor.Center), style: null);
            var filterArea = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.05f), paddedTab.RectTransform), isHorizontal: true, spacing: 5)
            {
                UserData = "filterarea"
            };
            new GUITextBlock(new RectTransform(new Vector2(0.25f, 1.0f), filterArea.RectTransform), TextManager.Get("FilterMapEntities"), font: GUI.SmallFont);
            entityFilterBox = new GUITextBox(new RectTransform(new Vector2(0.7f, 1.0f), filterArea.RectTransform), font: GUI.SmallFont)
            {
                OnTextChanged = (textBox, text) => { FilterEntities(text); return true; }
            };
            var clearButton = new GUIButton(new RectTransform(new Vector2(0.05f, 1.0f), filterArea.RectTransform), "x")
            {
                OnClicked = (btn, userdata) => { ClearFilter(); return true; }
            };
            entityList = new GUIListBox(new RectTransform(new Vector2(1.0f, 0.95f), paddedTab.RectTransform, Anchor.BottomCenter))
            {
                OnSelected = SelectPrefab,
                CheckSelected = MapEntityPrefab.GetSelected
            };
            UpdateEntityList();                       

            //empty guiframe as a separator
            new GUIFrame(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), style: null);

            button = new GUIButton(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), TextManager.Get("CharacterModeButton"))
            {
                ToolTip = TextManager.Get("CharacterModeToolTip"),
                OnClicked = ToggleCharacterMode
            };
            button = new GUIButton(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), TextManager.Get("WiringModeButton"))
            {
                ToolTip = TextManager.Get("WiringModeToolTip"),
                OnClicked = ToggleWiringMode
            };
            button = new GUIButton(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), TextManager.Get("GenerateWaypointsButton"))
            {
                ToolTip = TextManager.Get("GenerateWaypointsToolTip"),
                OnClicked = GenerateWaypoints
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.05f), paddedLeftPanel.RectTransform), TextManager.Get("ShowEntitiesLabel"));

            var tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.03f), paddedLeftPanel.RectTransform), TextManager.Get("ShowLighting"))
            {
                OnSelected = (GUITickBox obj) => { lightingEnabled = !lightingEnabled; return true; }
            };
            tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.03f), paddedLeftPanel.RectTransform), TextManager.Get("ShowWaypoints"))
            {
                OnSelected = (GUITickBox obj) => { WayPoint.ShowWayPoints = !WayPoint.ShowWayPoints; return true; },
                Selected = true
            };
            tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.03f), paddedLeftPanel.RectTransform), TextManager.Get("ShowSpawnpoints"))
            {
                OnSelected = (GUITickBox obj) => { WayPoint.ShowSpawnPoints = !WayPoint.ShowSpawnPoints; return true; },
                Selected = true
            };
            tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.03f), paddedLeftPanel.RectTransform), TextManager.Get("ShowLinks"))
            {
                OnSelected = (GUITickBox obj) => { Item.ShowLinks = !Item.ShowLinks; return true; },
                Selected = true
            };
            tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.03f), paddedLeftPanel.RectTransform), TextManager.Get("ShowHulls"))
            {
                OnSelected = (GUITickBox obj) => { Hull.ShowHulls = !Hull.ShowHulls; return true; },
                Selected = true
            };
            tickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.03f), paddedLeftPanel.RectTransform), TextManager.Get("ShowGaps"))
            {
                OnSelected = (GUITickBox obj) => { Gap.ShowGaps = !Gap.ShowGaps; return true; },
                Selected = true
            };

            //empty guiframe as a separator
            new GUIFrame(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), style: null);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.025f), paddedLeftPanel.RectTransform), TextManager.Get("PreviouslyUsedLabel"));
            previouslyUsedList = new GUIListBox(new RectTransform(new Vector2(1.0f, 0.2f), paddedLeftPanel.RectTransform))
            {
                OnSelected = SelectPrefab
            };
        }

        private void UpdateEntityList()
        {
            entityList.ClearChildren();

            foreach (MapEntityPrefab ep in MapEntityPrefab.List)
            {
                GUIFrame frame = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.1f), entityList.RectTransform) { MinSize = new Point(0, 50) },
                    style: "ListBoxElement")
                {
                    UserData = ep,
                    ToolTip = ep.Description
                };

                GUITextBlock textBlock = new GUITextBlock(new RectTransform(new Vector2(0.1f, 0.0f), frame.RectTransform) { AbsoluteOffset = new Point(frame.Rect.Height + 5, 0) }, 
                    text: ep.Name, textAlignment: Alignment.CenterLeft);
                if (!string.IsNullOrWhiteSpace(ep.Description))
                {
                    textBlock.ToolTip = ep.Description;
                }

                if (ep.sprite != null)
                {
                    GUIImage img = new GUIImage(new RectTransform(new Point(frame.Rect.Height, frame.Rect.Height), frame.RectTransform), ep.sprite);
                    img.Scale = Math.Min(Math.Min(40.0f / img.SourceRect.Width, 40.0f / img.SourceRect.Height), 1.0f);
                    img.Color = ep.SpriteColor;
                }

                if (ep.Category == MapEntityCategory.ItemAssembly)
                {
                    var deleteButton = new GUIButton(new RectTransform(new Vector2(0.15f, 0.5f), frame.RectTransform, Anchor.CenterRight) { MinSize = new Point(100, 20) },
                        TextManager.Get("Delete"))
                    {
                        UserData = ep,
                        OnClicked = (btn, userData) =>
                        {
                            ItemAssemblyPrefab assemblyPrefab = userData as ItemAssemblyPrefab;
                            assemblyPrefab.Delete();
                            UpdateEntityList();
                            return true;
                        }
                    };
                }
            }

            entityList.Children.Sort((i1, i2) => (i1.UserData as MapEntityPrefab).Name.CompareTo((i2.UserData as MapEntityPrefab).Name));
        }

        /*public void StartTutorial()
        {
            tutorial = new Tutorials.EditorTutorial("EditorTutorial");

            CoroutineManager.StartCoroutine(tutorial.UpdateState());
        }*/

        public override void Select()
        {
            base.Select();

            GUI.ForceMouseOn(null);
            characterMode = false;

            if (Submarine.MainSub != null)
            {
                cam.Position = Submarine.MainSub.Position + Submarine.MainSub.HiddenSubPosition;
            }
            else
            {
                Submarine.MainSub = new Submarine(Path.Combine(Submarine.SavePath, "Unnamed.sub"), "", false);
                cam.Position = Submarine.MainSub.Position;
            }

            SoundPlayer.OverrideMusicType = "none";
            SoundPlayer.OverrideMusicDuration = null;
            GameMain.SoundManager.SetCategoryGainMultiplier("default", 0.0f);
            GameMain.SoundManager.SetCategoryGainMultiplier("waterambience", 0.0f);

            linkedSubBox.ClearChildren();
            foreach (Submarine sub in Submarine.SavedSubmarines)
            {
                linkedSubBox.AddItem(sub.Name, sub);
            }

            cam.UpdateTransform();
        }

        public override void Deselect()
        {
            base.Deselect();

            GUI.ForceMouseOn(null);

            MapEntityPrefab.Selected = null;

            MapEntity.DeselectAll();

            if (characterMode) ToggleCharacterMode();

            if (wiringMode) ToggleWiringMode();

            SoundPlayer.OverrideMusicType = null;
            GameMain.SoundManager.SetCategoryGainMultiplier("default", GameMain.Config.SoundVolume);
            GameMain.SoundManager.SetCategoryGainMultiplier("waterambience", GameMain.Config.SoundVolume);

            if (dummyCharacter != null)
            {
                dummyCharacter.Remove();
                dummyCharacter = null;
                GameMain.World.ProcessChanges();
            }
        }

        private void CreateDummyCharacter()
        {
            if (dummyCharacter != null) RemoveDummyCharacter();

            dummyCharacter = Character.Create(Character.HumanConfigFile, Vector2.Zero, "");

            for (int i = 0; i<dummyCharacter.Inventory.SlotPositions.Length; i++)
            {
                dummyCharacter.Inventory.SlotPositions[i].X += leftPanel.Rect.Width+10;
            }

            Character.Controlled = dummyCharacter;
            GameMain.World.ProcessChanges();
        }

        private bool SaveSub(GUIButton button, object obj)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                GUI.AddMessage(TextManager.Get("SubNameMissingWarning"), Color.Red, 3.0f);

                nameBox.Flash();
                return false;
            }
            
            foreach (char illegalChar in Path.GetInvalidFileNameChars())
            {
                if (nameBox.Text.Contains(illegalChar))
                {
                    GUI.AddMessage(TextManager.Get("SubNameIllegalCharsWarning").Replace("[illegalchar]", illegalChar.ToString()), Color.Red, 3.0f);
                    nameBox.Flash();
                    return false;
                }
            }
            
            string savePath = nameBox.Text + ".sub";
            if (Submarine.MainSub != null)
            {
                savePath = Path.Combine(Path.GetDirectoryName(Submarine.MainSub.FilePath), savePath);
            }
            else
            {
                savePath = Path.Combine(Submarine.SavePath, savePath);
            }

            Submarine.MainSub.CompatibleContentPackages.Add(GameMain.Config.SelectedContentPackage.Name);

            MemoryStream imgStream = new MemoryStream();
            CreateImage(256, 128, imgStream);
            
            Submarine.SaveCurrent(savePath, imgStream);
            Submarine.MainSub.CheckForErrors();
            
            GUI.AddMessage(TextManager.Get("SubSavedNotification").Replace("[filepath]", Submarine.MainSub.FilePath), Color.Green, 3.0f);

            Submarine.RefreshSavedSubs();
            linkedSubBox.ClearChildren();
            foreach (Submarine sub in Submarine.SavedSubmarines)
            {
                linkedSubBox.AddItem(sub.Name, sub);
            }

            saveFrame = null;
            
            return false;
        }

        private void CreateSaveScreen()
        {
            if (characterMode) ToggleCharacterMode();
            if (wiringMode) ToggleWiringMode();

            int width = 400, height = 400;
            int y = 0;

            saveFrame = new GUIFrame(new RectTransform(new Vector2(0.25f, 0.36f), GUI.Canvas, Anchor.Center) { MinSize = new Point(400, 400) });
            GUIFrame paddedSaveFrame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), saveFrame.RectTransform, Anchor.Center), style: null);

            var header = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), paddedSaveFrame.RectTransform), TextManager.Get("SaveSubDialogHeader"), font: GUI.LargeFont);

            y += header.Rect.Height;

            var saveSubLabel = new GUITextBlock(new RectTransform(new Vector2(0.4f, 0.05f), paddedSaveFrame.RectTransform) { AbsoluteOffset = new Point(0, y) },
                TextManager.Get("SaveSubDialogName"));
            y += saveSubLabel.Rect.Height;

            nameBox = new GUITextBox(new RectTransform(new Vector2(0.65f, 0.05f), paddedSaveFrame.RectTransform) { AbsoluteOffset = new Point(0, y) });
            nameBox.OnEnterPressed = ChangeSubName;
            nameBox.Text = GetSubName();

            y += 30;
            
            new GUITextBlock(new Rectangle(0, y, 150, 20), TextManager.Get("SaveSubDialogDescription"), "", saveFrame);
            y += 20;

            var descriptionBox = new GUITextBox(new Rectangle(5, y, 0, 100), null, null, Alignment.TopLeft,
                Alignment.TopLeft, "", saveFrame);
            descriptionBox.Wrap = true;
            descriptionBox.Text = Submarine.MainSub == null ? "" : Submarine.MainSub.Description;
            descriptionBox.OnTextChanged = ChangeSubDescription;

            y += descriptionBox.Rect.Height;
            new GUITextBlock(new Rectangle(0, y, 150, 20), TextManager.Get("SaveSubDialogSettings"), "", saveFrame);

            y += 30;

            int tagX = 10, tagY = 0;
            foreach (SubmarineTag tag in Enum.GetValues(typeof(SubmarineTag)))
            {
                FieldInfo fi = typeof(SubmarineTag).GetField(tag.ToString());
                DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

                string tagStr = attributes.Length > 0 ? attributes[0].Description : "";

                var tagTickBox = new GUITickBox(new Rectangle(tagX, y + tagY, 20, 20), tagStr, Alignment.TopLeft, saveFrame);
                tagTickBox.Selected = Submarine.MainSub == null ? false : Submarine.MainSub.HasTag(tag);
                tagTickBox.UserData = tag;

                tagTickBox.OnSelected = (GUITickBox tickBox) =>
                    {
                        if (Submarine.MainSub == null) return false;

                        if (tickBox.Selected)
                        {
                            Submarine.MainSub.AddTag((SubmarineTag)tickBox.UserData);
                        }
                        else
                        {
                            Submarine.MainSub.RemoveTag((SubmarineTag)tickBox.UserData);
                        }

                        return true;
                    };

                tagY += 25;
                if (tagY > 100)
                {
                    tagY = 0;
                    tagX += 200;
                }
            }

            y -= 30;

            new GUITextBlock(new Rectangle(160, y, 100, 20), TextManager.Get("CompatibleContentPackages"), "", saveFrame);
            var contentPackList = new GUIListBox(new Rectangle(160, y + 30, 0, 50), "", saveFrame);
            List<string> contentPacks = Submarine.MainSub.CompatibleContentPackages.ToList();
            foreach (ContentPackage contentPack in ContentPackage.list)
            {
                if (!contentPacks.Contains(contentPack.Name)) contentPacks.Add(contentPack.Name);
            }

            foreach (string contentPackageName in contentPacks)
            {
                var cpTickBox = new GUITickBox(new Rectangle(0, 0, 15, 15), contentPackageName, Alignment.TopLeft, GUI.SmallFont, "", contentPackList);
                cpTickBox.Selected = Submarine.MainSub.CompatibleContentPackages.Contains(contentPackageName);
                cpTickBox.UserData = contentPackageName;
                cpTickBox.OnSelected += (GUITickBox tickBox) =>
                {
                    if (tickBox.Selected)
                    {
                        Submarine.MainSub.CompatibleContentPackages.Add((string)tickBox.UserData);
                    }
                    else
                    {
                        Submarine.MainSub.CompatibleContentPackages.Remove((string)tickBox.UserData);
                    }
                    return true;
                };
            }

            y += 90;

            new GUITextBlock(new Rectangle(0, y, 100, 20), TextManager.Get("RecommendedCrewSize"), "", Alignment.TopLeft, Alignment.CenterLeft, saveFrame);

            var crewSizeMin = new GUINumberInput(new Rectangle(230, y, 50, 20), "", GUINumberInput.NumberType.Int, saveFrame);
            crewSizeMin.MinValueInt = 1;
            crewSizeMin.MaxValueInt = 128;

            new GUITextBlock(new Rectangle(285, y, 10, 20), "-", "", Alignment.TopLeft, Alignment.Center, saveFrame);

            var crewSizeMax = new GUINumberInput(new Rectangle(300, y, 50, 20), "", GUINumberInput.NumberType.Int, saveFrame);
            crewSizeMax.MinValueInt = 1;
            crewSizeMax.MaxValueInt = 128;

            crewSizeMin.OnValueChanged += (numberInput) =>
            {
                crewSizeMax.IntValue = Math.Max(crewSizeMax.IntValue, numberInput.IntValue);
                Submarine.MainSub.RecommendedCrewSizeMin = crewSizeMin.IntValue;
                Submarine.MainSub.RecommendedCrewSizeMax = crewSizeMax.IntValue;
            };

            crewSizeMax.OnValueChanged += (numberInput) =>
            {
                crewSizeMin.IntValue = Math.Min(crewSizeMin.IntValue, numberInput.IntValue);
                Submarine.MainSub.RecommendedCrewSizeMin = crewSizeMin.IntValue;
                Submarine.MainSub.RecommendedCrewSizeMax = crewSizeMax.IntValue;
            };

            y += 20;

            new GUITextBlock(new Rectangle(0, y, 100, 20), TextManager.Get("RecommendedCrewExperience"), "", Alignment.TopLeft, Alignment.CenterLeft, saveFrame);

            var toggleExpLeft = new GUIButton(new Rectangle(230, y, 20, 20), "<", "", saveFrame);
            var toggleExpRight = new GUIButton(new Rectangle(350, y, 20, 20), ">", "", saveFrame);
            var experienceText = new GUITextBlock(new Rectangle(250, y, 100, 20), crewExperienceLevels[0], "", Alignment.TopLeft, Alignment.Center, saveFrame);

            toggleExpLeft.OnClicked += (btn, userData) =>
            {
                int currentIndex = Array.IndexOf(crewExperienceLevels, experienceText.Text);
                currentIndex--;
                if (currentIndex < 0) currentIndex = crewExperienceLevels.Length - 1;
                experienceText.Text = crewExperienceLevels[currentIndex];
                Submarine.MainSub.RecommendedCrewExperience = experienceText.Text;
                return true;
            };

            toggleExpRight.OnClicked += (btn, userData) =>
            {
                int currentIndex = Array.IndexOf(crewExperienceLevels, experienceText.Text);
                currentIndex++;
                if (currentIndex >= crewExperienceLevels.Length) currentIndex = 0;
                experienceText.Text = crewExperienceLevels[currentIndex];
                Submarine.MainSub.RecommendedCrewExperience = experienceText.Text;
                return true;
            };

            if (Submarine.MainSub != null)
            {
                int min =  Submarine.MainSub.RecommendedCrewSizeMin;
                int max = Submarine.MainSub.RecommendedCrewSizeMax;
                crewSizeMin.IntValue = min;
                crewSizeMax.IntValue = max;
                experienceText.Text = string.IsNullOrEmpty(Submarine.MainSub.RecommendedCrewExperience) ?
                    crewExperienceLevels[0] : Submarine.MainSub.RecommendedCrewExperience;
            }

            var saveButton = new GUIButton(new Rectangle(-90, 0, 80, 20), TextManager.Get("SaveSubButton"), Alignment.Right | Alignment.Bottom, "", saveFrame);
            saveButton.OnClicked = SaveSub;

            var cancelButton = new GUIButton(new Rectangle(0, 0, 80, 20), TextManager.Get("Cancel"), Alignment.Right | Alignment.Bottom, "", saveFrame);
            cancelButton.OnClicked = (GUIButton btn, object userdata) =>
            {
                saveFrame = null;
                return true;
            };
        }


        private void CreateSaveAssemblyScreen()
        {
            if (characterMode) ToggleCharacterMode();
            if (wiringMode) ToggleWiringMode();

            int width = 400, height = 210;
            int y = 0;

            saveFrame = new GUIFrame(new Rectangle(GameMain.GraphicsWidth / 2 - width / 2, GameMain.GraphicsHeight / 2 - height / 2, width, height), "", null);
            saveFrame.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);

            new GUITextBlock(new Rectangle(0, 0, 200, 30), TextManager.Get("SaveItemAssemblyDialogHeader"), "", saveFrame, GUI.LargeFont);
            y += 30;

            new GUITextBlock(new Rectangle(0, y, 150, 20), TextManager.Get("SaveItemAssemblyDialogName"), "", saveFrame);
            y += 20;

            nameBox = new GUITextBox(new Rectangle(5, y, 250, 20), "", saveFrame);
            y += 30;

            new GUITextBlock(new Rectangle(0, y, 150, 20), TextManager.Get("SaveItemAssemblyDialogDescription"), "", saveFrame);
            y += 20;

            var descriptionBox = new GUITextBox(new Rectangle(5, y, 0, 80), null, null, Alignment.TopLeft,
                Alignment.TopLeft, "", saveFrame);
            descriptionBox.UserData = "description";
            descriptionBox.Wrap = true;
            descriptionBox.Text = "";
                        
            var saveButton = new GUIButton(new Rectangle(-90, 0, 80, 20), TextManager.Get("SaveSubButton"), Alignment.Right | Alignment.Bottom, "", saveFrame);
            saveButton.OnClicked = SaveAssembly;

            var cancelButton = new GUIButton(new Rectangle(0, 0, 80, 20), TextManager.Get("Cancel"), Alignment.Right | Alignment.Bottom, "", saveFrame);
            cancelButton.OnClicked = (GUIButton btn, object userdata) =>
            {
                saveFrame = null;
                return true;
            };
        }

        private bool SaveAssembly(GUIButton button, object obj)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                GUI.AddMessage(TextManager.Get("ItemAssemblyNameMissingWarning"), Color.Red, 3.0f);

                nameBox.Flash();
                return false;
            }

            foreach (char illegalChar in Path.GetInvalidFileNameChars())
            {
                if (nameBox.Text.Contains(illegalChar))
                {
                    GUI.AddMessage(TextManager.Get("ItemAssemblyNameIllegalCharsWarning").Replace("[illegalchar]", illegalChar.ToString()), Color.Red, 3.0f);
                    nameBox.Flash();
                    return false;
                }
            }

            string description = ((GUITextBox)saveFrame.GetChild("description")).Text;

            string saveFolder = Path.Combine("Content", "Items", "Assemblies");
            XDocument doc = new XDocument(ItemAssemblyPrefab.Save(MapEntity.SelectedList, nameBox.Text, description));
            string filePath = Path.Combine(saveFolder, nameBox.Text + ".xml");
            doc.Save(filePath);

            new ItemAssemblyPrefab(filePath);
            UpdateEntityList();
            saveFrame = null;
            return false;
        }

        private bool CreateLoadScreen()
        {
            if (characterMode) ToggleCharacterMode();
            if (wiringMode) ToggleWiringMode();

            Submarine.RefreshSavedSubs();

            int width = 300, height = 400;
            loadFrame = new GUIFrame(new Rectangle(GameMain.GraphicsWidth / 2 - width / 2, GameMain.GraphicsHeight / 2 - height / 2, width, height), "", null);
            loadFrame.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);

            var subList = new GUIListBox(new Rectangle(0, 0, 0, height - 50), Color.White, "", loadFrame);
            subList.OnSelected = (GUIComponent selected, object userData) =>
                {
                    var deleteBtn = loadFrame.FindChild("delete") as GUIButton;
                    if (deleteBtn != null) deleteBtn.Enabled = true;

                    return true;
                };

            foreach (Submarine sub in Submarine.SavedSubmarines)
            {
                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(0, 0, 0, 25),
                    ToolBox.LimitString(sub.Name, GUI.Font, subList.Rect.Width - 80),
                    "",
                    Alignment.Left, Alignment.CenterY | Alignment.Left, subList);
                textBlock.Padding = new Vector4(10.0f, 0.0f, 0.0f, 0.0f);
                textBlock.UserData = sub;
                textBlock.ToolTip = sub.FilePath;

                if (sub.HasTag(SubmarineTag.Shuttle))
                {
                    var shuttleText = new GUITextBlock(new Rectangle(0, 0, 0, 25), TextManager.Get("Shuttle"), "", Alignment.Left, Alignment.CenterY | Alignment.Right, textBlock, false, GUI.SmallFont);
                    shuttleText.TextColor = textBlock.TextColor * 0.8f;
                    shuttleText.ToolTip = textBlock.ToolTip;
                }
            }

            var deleteButton = new GUIButton(new Rectangle(0, 0, 70, 20), TextManager.Get("Delete"), Alignment.BottomLeft, "", loadFrame);
            deleteButton.Enabled = false;
            deleteButton.UserData = "delete";
            deleteButton.OnClicked = (btn, userdata) =>
            {
                if (subList.Selected != null)
                {
                    TryDeleteSub(subList.Selected.UserData as Submarine);
                }

                deleteButton.Enabled = false;
                
                return true;
            };

            var loadButton = new GUIButton(new Rectangle(-90, 0, 80, 20), TextManager.Get("Load"), Alignment.Right | Alignment.Bottom, "", loadFrame);
            loadButton.OnClicked = LoadSub;

            var cancelButton = new GUIButton(new Rectangle(0, 0, 80, 20), TextManager.Get("Cancel"), Alignment.Right | Alignment.Bottom, "", loadFrame);
            cancelButton.OnClicked = (GUIButton btn, object userdata) =>
                {
                    loadFrame = null;
                    return true;
                };

            return true;
        }

        private bool LoadSub(GUIButton button, object obj)
        {
            // TODO: loadFrame is null
            GUIListBox subList = loadFrame.GetChild<GUIListBox>();

            if (subList.Selected == null) return false;

            Submarine selectedSub = subList.Selected.UserData as Submarine;

            if (selectedSub == null) return false;

            Submarine.MainSub = selectedSub;
            selectedSub.Load(true);

            cam.Position = Submarine.MainSub.Position + Submarine.MainSub.HiddenSubPosition;

            loadFrame = null;

            return true;
        }

        private void TryDeleteSub(Submarine sub)
        {
            if (sub == null) return;
            
            var msgBox = new GUIMessageBox(
                TextManager.Get("DeleteDialogLabel"),
                TextManager.Get("DeleteDialogQuestion").Replace("[file]", sub.Name), 
                new string[] { TextManager.Get("Yes"), TextManager.Get("Cancel") });
            msgBox.Buttons[0].OnClicked += (btn, userData) => 
            {
                try
                {
                    sub.Remove();
                    File.Delete(sub.FilePath);
                    CreateLoadScreen();
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError(TextManager.Get("DeleteFileError").Replace("[file]", sub.FilePath), e);
                }
                return true;
            };
            msgBox.Buttons[0].OnClicked += msgBox.Close;
            msgBox.Buttons[1].OnClicked += msgBox.Close;            
        }

        private bool OpenEntityMenu(MapEntityCategory selectedCategory)
        {
            if (characterMode) ToggleCharacterMode();
            if (wiringMode) ToggleWiringMode();

            saveFrame = null;
            loadFrame = null;

            ClearFilter();
            entityFilterBox.Text = "";
            entityFilterBox.Select();

            foreach (GUIComponent child in entityList.Children)
            {
                child.Visible = ((MapEntityPrefab)child.UserData).Category == selectedCategory;
            }

            return true;
        }

        private bool FilterEntities(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                entityList.Children.ForEach(c => c.Visible = true);
                return true;
            }

            filter = filter.ToLower();
            foreach (GUIComponent child in entityList.Children)
            {
                var textBlock = child.GetChild<GUITextBlock>();
                child.Visible = ((MapEntityPrefab)child.UserData).Name.ToLower().Contains(filter);
            }
            entityList.BarScroll = 0.0f;

            return true;
        }

        public bool ClearFilter()
        {
            FilterEntities("");
            entityFilterBox.Text = "";
            return true;
        }

        public void ToggleCharacterMode()
        {
            ToggleCharacterMode(null,null);
        }

        private bool ToggleCharacterMode(GUIButton button, object obj)
        {
            entityMenuOpen = false;
            wiringMode = false;
            characterMode = !characterMode;

            if (characterMode)
            {
                CreateDummyCharacter();
            }
            else if (dummyCharacter != null)
            {
                RemoveDummyCharacter();
            }

            foreach (MapEntity me in MapEntity.mapEntityList)
            {
                me.IsHighlighted = false;
            }

            MapEntity.DeselectAll();
            
            return true;
        }

        private void ToggleWiringMode()
        {
            ToggleWiringMode(null, null);
        }
        
        private bool ToggleWiringMode(GUIButton button, object obj)
        {
            wiringMode = !wiringMode;

            characterMode = false;

            if (wiringMode)
            {
                CreateDummyCharacter();
                var item = new Item(MapEntityPrefab.Find("Screwdriver") as ItemPrefab, Vector2.Zero, null);
                dummyCharacter.Inventory.TryPutItem(item, null, new List<InvSlotType>() { InvSlotType.RightHand });
                wiringToolPanel = CreateWiringPanel();
            }
            else
            {
                RemoveDummyCharacter();
            }

            MapEntity.DeselectAll();
            
            return true;
        }

        private void RemoveDummyCharacter()
        {
            if (dummyCharacter == null) return;
            
            foreach (Item item in dummyCharacter.Inventory.Items)
            {
                if (item == null) continue;

                item.Remove();
            }

            dummyCharacter.Remove();
            dummyCharacter = null;
            
        }

        private GUIFrame CreateWiringPanel()
        {
            GUIFrame frame = new GUIFrame(new Rectangle(0, 0, 65, 300), null, Alignment.Right | Alignment.CenterY, "GUIFrameRight");
            frame.Padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f);

            GUIListBox listBox = new GUIListBox(Rectangle.Empty, "", frame);
            listBox.OnSelected = SelectWire;
            
            foreach (MapEntityPrefab ep in MapEntityPrefab.List)
            {
                var itemPrefab = ep as ItemPrefab;
                if (itemPrefab == null || itemPrefab.Name == null) continue;
                if (!itemPrefab.Name.Contains("Wire") && (itemPrefab.Aliases == null || !itemPrefab.Aliases.Any(a => a.Contains("Wire")))) continue;

                GUIFrame imgFrame = new GUIFrame(new Rectangle(0, 0, (int)itemPrefab.sprite.size.X, (int)itemPrefab.sprite.size.Y), null, listBox);
                imgFrame.UserData = itemPrefab;
                imgFrame.HoverColor = Color.White * 0.5f;
                imgFrame.SelectedColor = Color.Gold * 0.7f;

                var img = new GUIImage(new Rectangle(0, 0, (int)itemPrefab.sprite.size.X, (int)itemPrefab.sprite.size.Y), itemPrefab.sprite, Alignment.TopLeft, imgFrame);
                img.Color = ep.SpriteColor;
            }

            return frame;
        }

        private bool SelectLinkedSub(GUIComponent selected, object userData)
        {
            var submarine = selected.UserData as Submarine;
            if (submarine == null) return false;

            var prefab = new LinkedSubmarinePrefab(submarine);

            MapEntityPrefab.SelectPrefab(prefab);

            return true;
        }

        private bool SelectWire(GUIComponent component, object userData)
        {
            if (dummyCharacter == null) return false;

            //if the same type of wire has already been selected, deselect it and return
            Item existingWire = dummyCharacter.SelectedItems.FirstOrDefault(i => i != null && i.Prefab == userData as ItemPrefab);
            if (existingWire != null)
            {
                existingWire.Drop();
                existingWire.Remove();
                return false;
            }

            var wire = new Item(userData as ItemPrefab, Vector2.Zero, null);

            int slotIndex = dummyCharacter.Inventory.FindLimbSlot(InvSlotType.LeftHand);

            //if there's some other type of wire in the inventory, remove it
            existingWire = dummyCharacter.Inventory.Items[slotIndex];
            if (existingWire != null && existingWire.Prefab != userData as ItemPrefab)
            {
                existingWire.Drop();
                existingWire.Remove();
            }

            dummyCharacter.Inventory.TryPutItem(wire, slotIndex, false, false, dummyCharacter);

            return true;
           
        }

        private bool ChangeSubName(GUITextBox textBox, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                textBox.Flash(Color.Red);
                return false;
            }

            if (Submarine.MainSub != null) Submarine.MainSub.Name = text;
            textBox.Deselect();

            textBox.Text = text;

            textBox.Flash(Color.Green);

            return true;
        }

        private bool ChangeSubDescription(GUITextBox textBox, string text)
        {
            if (Submarine.MainSub != null)
            {
                Submarine.MainSub.Description = text;
            }
            else
            {
                textBox.UserData = text;
            }

            // textBox.Rect = new Rectangle(textBox.Rect.Location, new Point(textBox.Rect.Width, 20));
            
            //textBox.Text = ToolBox.LimitString(text, 15);

            //textBox.Flash(Color.Green);
            //textBox.Deselect();

            return true;
        }
        
        private bool SelectPrefab(GUIComponent component, object obj)
        {
            AddPreviouslyUsed(obj as MapEntityPrefab);

            MapEntityPrefab.SelectPrefab(obj);
            entityMenuOpen = false;
            GUI.ForceMouseOn(null);
            return false;
        }

        private bool GenerateWaypoints(GUIButton button, object obj)
        {
            if (Submarine.MainSub == null) return false;

            WayPoint.GenerateSubWaypoints(Submarine.MainSub);
            return true;
        }

        private void AddPreviouslyUsed(MapEntityPrefab mapEntityPrefab)
        {
            if (previouslyUsedList == null || mapEntityPrefab == null) return;

            previouslyUsedList.Deselect();

            if (previouslyUsedList.CountChildren == PreviouslyUsedCount)
            {
                previouslyUsedList.RemoveChild(previouslyUsedList.Children.Last());
            }

            var existing = previouslyUsedList.FindChild(mapEntityPrefab);
            if (existing != null) previouslyUsedList.RemoveChild(existing);

            string name = ToolBox.LimitString(mapEntityPrefab.Name,15);

            var textBlock = new GUITextBlock(
                new Rectangle(0,0,0,15), 
                ToolBox.LimitString(name, GUI.SmallFont, previouslyUsedList.Rect.Width),
                "", Alignment.TopLeft, Alignment.CenterLeft, 
                previouslyUsedList, false, GUI.SmallFont);

            textBlock.UserData = mapEntityPrefab;

            previouslyUsedList.RemoveChild(textBlock);
            previouslyUsedList.Children.Insert(0, textBlock);
        }
        
        public void AutoHull()
        {
            for (int i = 0; i < MapEntity.mapEntityList.Count; i++)
            {
                MapEntity h = MapEntity.mapEntityList[i];
                if (h is Hull || h is Gap)
                {
                    h.Remove();
                    i--;
                }
            }

            List<Vector2> wallPoints = new List<Vector2>();
            Vector2 min = Vector2.Zero;
            Vector2 max = Vector2.Zero;

            List<MapEntity> mapEntityList = new List<MapEntity>();

            foreach (MapEntity e in MapEntity.mapEntityList)
            {
                if (e is Item)
                {
                    Item it = e as Item;
                    Door door = it.GetComponent<Door>();
                    if (door != null)
                    {
                        int halfW = e.WorldRect.Width / 2;
                        wallPoints.Add(new Vector2(e.WorldRect.X + halfW, -e.WorldRect.Y + e.WorldRect.Height));
                        mapEntityList.Add(it);
                    }
                    continue;
                }

                if (!(e is Structure)) continue;
                Structure s = e as Structure;
                if (!s.HasBody) continue;
                mapEntityList.Add(e);

                if (e.Rect.Width > e.Rect.Height)
                {
                    int halfH = e.WorldRect.Height / 2;
                    wallPoints.Add(new Vector2(e.WorldRect.X, -e.WorldRect.Y + halfH));
                    wallPoints.Add(new Vector2(e.WorldRect.X + e.WorldRect.Width, -e.WorldRect.Y + halfH));
                }
                else
                {
                    int halfW = e.WorldRect.Width / 2;
                    wallPoints.Add(new Vector2(e.WorldRect.X + halfW, -e.WorldRect.Y));
                    wallPoints.Add(new Vector2(e.WorldRect.X + halfW, -e.WorldRect.Y + e.WorldRect.Height));
                }
            }

            min = wallPoints[0];
            max = wallPoints[0];
            for (int i = 0; i < wallPoints.Count; i++)
            {
                min.X = Math.Min(min.X, wallPoints[i].X);
                min.Y = Math.Min(min.Y, wallPoints[i].Y);
                max.X = Math.Max(max.X, wallPoints[i].X);
                max.Y = Math.Max(max.Y, wallPoints[i].Y);
            }

            List<Rectangle> hullRects = new List<Rectangle>();
            hullRects.Add(new Rectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y)));
            foreach (Vector2 point in wallPoints)
            {
                MathUtils.SplitRectanglesHorizontal(hullRects, point);
                MathUtils.SplitRectanglesVertical(hullRects, point);
            }

            hullRects.Sort((a, b) =>
            {
                if (a.Y < b.Y) return -1;
                if (a.Y > b.Y) return 1;
                if (a.X < b.X) return -1;
                if (a.X > b.X) return 1;
                return 0;
            });

            for (int i = 0; i < hullRects.Count - 1; i++)
            {
                Rectangle rect = hullRects[i];
                if (hullRects[i + 1].Y > rect.Y) continue;

                Vector2 hullRPoint = new Vector2(rect.X + rect.Width - 8, rect.Y + rect.Height / 2);
                Vector2 hullLPoint = new Vector2(rect.X, rect.Y + rect.Height / 2);

                MapEntity container = null;
                foreach (MapEntity e in mapEntityList)
                {
                    Rectangle entRect = e.WorldRect;
                    entRect.Y = -entRect.Y;
                    if (entRect.Contains(hullRPoint))
                    {
                        if (!entRect.Contains(hullLPoint)) container = e;
                        break;
                    }
                }
                if (container == null)
                {
                    rect.Width += hullRects[i + 1].Width;
                    hullRects[i] = rect;
                    hullRects.RemoveAt(i + 1);
                    i--;
                }
            }
            
            foreach (MapEntity e in mapEntityList)
            {
                Rectangle entRect = e.WorldRect;
                if (entRect.Width < entRect.Height) continue;
                entRect.Y = -entRect.Y - 16;
                for (int i = 0; i < hullRects.Count; i++)
                {
                    Rectangle hullRect = hullRects[i];
                    if (entRect.Intersects(hullRect))
                    {
                        if (hullRect.Y < entRect.Y)
                        {
                            hullRect.Height = Math.Max((entRect.Y + 16 + entRect.Height / 2) - hullRect.Y, hullRect.Height);
                            hullRects[i] = hullRect;
                        }
                        else if (hullRect.Y + hullRect.Height <= entRect.Y + 16 + entRect.Height)
                        {
                            hullRects.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            foreach (MapEntity e in mapEntityList)
            {
                Rectangle entRect = e.WorldRect;
                if (entRect.Width < entRect.Height) continue;
                entRect.Y = -entRect.Y;
                for (int i = 0; i < hullRects.Count; i++)
                {
                    Rectangle hullRect = hullRects[i];
                    if (entRect.Intersects(hullRect))
                    {
                        if (hullRect.Y >= entRect.Y - 8 && hullRect.Y + hullRect.Height <= entRect.Y + entRect.Height + 8)
                        {
                            hullRects.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            
            for (int i = 0; i < hullRects.Count;)
            {
                Rectangle hullRect = hullRects[i];
                Vector2 point = new Vector2(hullRect.X+2, hullRect.Y+hullRect.Height/2);
                MapEntity container = null;
                foreach (MapEntity e in mapEntityList)
                {
                    Rectangle entRect = e.WorldRect;
                    entRect.Y = -entRect.Y;
                    if (entRect.Contains(point))
                    {
                        container = e;
                        break;
                    }
                }
                if (container == null)
                {
                    hullRects.RemoveAt(i);
                    continue;
                }

                while (hullRects[i].Y <= hullRect.Y)
                {
                    i++;
                    if (i >= hullRects.Count) break;
                }
            }
            
            for (int i = hullRects.Count-1; i >= 0;)
            {
                Rectangle hullRect = hullRects[i];
                Vector2 point = new Vector2(hullRect.X+hullRect.Width-2, hullRect.Y+hullRect.Height/2);
                MapEntity container = null;
                foreach (MapEntity e in mapEntityList)
                {
                    Rectangle entRect = e.WorldRect;
                    entRect.Y = -entRect.Y;
                    if (entRect.Contains(point))
                    {
                        container = e;
                        break;
                    }
                }
                if (container == null)
                {
                    hullRects.RemoveAt(i); i--;
                    continue;
                }

                while (hullRects[i].Y >= hullRect.Y)
                {
                    i--;
                    if (i < 0) break;
                }
            }
            
            hullRects.Sort((a, b) =>
            {
                if (a.X < b.X) return -1;
                if (a.X > b.X) return 1;
                if (a.Y < b.Y) return -1;
                if (a.Y > b.Y) return 1;
                return 0;
            });
            
            for (int i = 0; i < hullRects.Count - 1; i++)
            {
                Rectangle rect = hullRects[i];
                if (hullRects[i + 1].Width != rect.Width) continue;
                if (hullRects[i + 1].X > rect.X) continue;

                Vector2 hullBPoint = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height - 8);
                Vector2 hullUPoint = new Vector2(rect.X + rect.Width / 2, rect.Y);

                MapEntity container = null;
                foreach (MapEntity e in mapEntityList)
                {
                    Rectangle entRect = e.WorldRect;
                    entRect.Y = -entRect.Y;
                    if (entRect.Contains(hullBPoint))
                    {
                        if (!entRect.Contains(hullUPoint)) container = e;
                        break;
                    }
                }
                if (container == null)
                {
                    rect.Height += hullRects[i + 1].Height;
                    hullRects[i] = rect;
                    hullRects.RemoveAt(i + 1);
                    i--;
                }
            }
            
            for (int i = 0; i < hullRects.Count;i++)
            {
                Rectangle rect = hullRects[i];
                rect.Y -= 16;
                rect.Height += 32;
                hullRects[i] = rect;
            }
            
            hullRects.Sort((a, b) =>
            {
                if (a.Y < b.Y) return -1;
                if (a.Y > b.Y) return 1;
                if (a.X < b.X) return -1;
                if (a.X > b.X) return 1;
                return 0;
            });
            
            for (int i = 0; i < hullRects.Count; i++)
            {
                for (int j = i+1; j < hullRects.Count; j++)
                {
                    if (hullRects[j].Y <= hullRects[i].Y) continue;
                    if (hullRects[j].Intersects(hullRects[i]))
                    {
                        Rectangle rect = hullRects[i];
                        rect.Height = hullRects[j].Y - rect.Y;
                        hullRects[i] = rect;
                        break;
                    }
                }
            }

            foreach (Rectangle rect in hullRects)
            {
                Rectangle hullRect = rect;
                hullRect.Y = -hullRect.Y;
                Hull newHull = new Hull(MapEntityPrefab.Find("Hull"),
                                        hullRect,
                                        Submarine.MainSub);
            }

            foreach (MapEntity e in mapEntityList)
            {
                if (!(e is Structure)) continue;
                if (!(e as Structure).IsPlatform) continue;

                Rectangle gapRect = e.WorldRect;
                gapRect.Y -= 8;
                gapRect.Height = 16;
                Gap newGap = new Gap(MapEntityPrefab.Find("Gap"),
                                        gapRect);
            }
        }
        
        public override void AddToGUIUpdateList()
        {
            if (tutorial != null) tutorial.AddToGUIUpdateList();

            if (MapEntity.SelectedList.Count == 1)
            {
                MapEntity.SelectedList[0].AddToGUIUpdateList();
            }
            if (MapEntity.HighlightedListBox != null)
            {
                MapEntity.HighlightedListBox.AddToGUIUpdateList();
            }

            leftPanel.AddToGUIUpdateList();
            topPanel.AddToGUIUpdateList();

            if (wiringMode)
            {
                wiringToolPanel.AddToGUIUpdateList();
            }

            if ((characterMode || wiringMode) && dummyCharacter != null)
            {
                CharacterHUD.AddToGUIUpdateList(dummyCharacter);
                if (dummyCharacter.SelectedConstruction != null)
                {
                    dummyCharacter.SelectedConstruction.AddToGUIUpdateList();
                }
            }
            else
            {
                if (loadFrame != null)
                {
                    loadFrame.AddToGUIUpdateList();
                }
                else if (saveFrame != null)
                {
                    saveFrame.AddToGUIUpdateList();
                }
                else if (entityMenuOpen)
                {
                    entityMenu.AddToGUIUpdateList();
                }
            }

            //GUI.AddToGUIUpdateList();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        public override void Update(double deltaTime)
        {
            if (tutorial != null) tutorial.Update((float)deltaTime);

            hullVolumeFrame.Visible = MapEntity.SelectedList.Any(s => s is Hull);
            saveAssemblyFrame.Visible = MapEntity.SelectedList.Count > 0;
            
            cam.MoveCamera((float)deltaTime, true, GUI.MouseOn == null);       
            if (PlayerInput.MidButtonHeld())
            {
                Vector2 moveSpeed = PlayerInput.MouseSpeed * (float)deltaTime * 100.0f / cam.Zoom;
                moveSpeed.X = -moveSpeed.X;
                cam.Position += moveSpeed;
            }

            if (characterMode || wiringMode)
            {
                if (dummyCharacter == null || Entity.FindEntityByID(dummyCharacter.ID) != dummyCharacter)
                {
                    ToggleCharacterMode(null, null);
                }
                else
                {
                    foreach (MapEntity me in MapEntity.mapEntityList)
                    {
                        me.IsHighlighted = false;
                    }

                    if (wiringMode && dummyCharacter.SelectedConstruction==null)
                    {
                        List<Wire> wires = new List<Wire>();
                        foreach (Item item in Item.ItemList)
                        {
                            var wire = item.GetComponent<Wire>();
                            if (wire != null) wires.Add(wire);
                        }
                        Wire.UpdateEditing(wires);
                    }

                    if (dummyCharacter.SelectedConstruction==null || dummyCharacter.SelectedConstruction.GetComponent<Pickable>() != null)
                    {
                        Vector2 mouseSimPos = FarseerPhysics.ConvertUnits.ToSimUnits(dummyCharacter.CursorPosition);
                        foreach (Limb limb in dummyCharacter.AnimController.Limbs)
                        {
                            limb.body.SetTransform(mouseSimPos, 0.0f);
                        }
                        dummyCharacter.AnimController.Collider.SetTransform(mouseSimPos, 0.0f);
                    }

                    dummyCharacter.ControlLocalPlayer((float)deltaTime, cam, false);
                    dummyCharacter.Control((float)deltaTime, cam);

                    dummyCharacter.Submarine = Submarine.MainSub;

                    cam.TargetPos = Vector2.Zero;
                }
            }
            else if (!saveAssemblyFrame.Rect.Contains(PlayerInput.MousePosition))
            {
                MapEntity.UpdateSelecting(cam);
            }

            //GUIComponent.ForceMouseOn(null);

            if (!characterMode && !wiringMode)
            {
                if (MapEntityPrefab.Selected != null) MapEntityPrefab.Selected.UpdatePlacing(cam);
                
                MapEntity.UpdateEditor(cam);
            }

            //leftPanel.Update((float)deltaTime);
            //topPanel.Update((float)deltaTime);

            if (wiringMode)
            {
                if (!dummyCharacter.SelectedItems.Any(it => it != null && it.HasTag("Wire")))
                {
                    wiringToolPanel.GetChild<GUIListBox>().Deselect();
                }
                //wiringToolPanel.Update((float)deltaTime);
            }
            
            if (loadFrame!=null)
            {
                //loadFrame.Update((float)deltaTime);
                if (PlayerInput.RightButtonClicked()) loadFrame = null;
            }
            else if (saveFrame != null)
            {
                //saveFrame.Update((float)deltaTime);
            }
            else if (entityMenuOpen)
            {
                //GUItabs[selectedTab].Update((float)deltaTime);
                if (PlayerInput.RightButtonClicked()) entityMenuOpen = false;
            }


            if ((characterMode || wiringMode) && dummyCharacter != null)
            {
                dummyCharacter.AnimController.FindHull(dummyCharacter.CursorWorldPosition, false);

                foreach (Item item in dummyCharacter.Inventory.Items)
                {
                    if (item == null) continue;

                    item.SetTransform(dummyCharacter.SimPosition, 0.0f);
                    item.UpdateTransform();
                    item.SetTransform(item.body.SimPosition, 0.0f);

                    //wires need to be updated for the last node to follow the player during rewiring
                    Wire wire = item.GetComponent<Wire>();
                    if (wire != null) wire.Update((float)deltaTime, cam);
                }

                if (dummyCharacter.SelectedConstruction != null)
                {
                    if (dummyCharacter.SelectedConstruction != null)
                    {
                        dummyCharacter.SelectedConstruction.UpdateHUD(cam, dummyCharacter, (float)deltaTime);
                    }

                    if (PlayerInput.KeyHit(InputType.Select) && dummyCharacter.FocusedItem != dummyCharacter.SelectedConstruction) dummyCharacter.SelectedConstruction = null;
                }

                CharacterHUD.Update((float)deltaTime, dummyCharacter);
            }

            //GUI.Update((float)deltaTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            cam.UpdateTransform();
            if (lightingEnabled)
            {
                GameMain.LightManager.UpdateLightMap(graphics, spriteBatch, cam, lightBlur.Effect);
            }

            spriteBatch.Begin(SpriteSortMode.BackToFront,
                BlendState.AlphaBlend,
                null, null, null, null,
                cam.Transform);

            graphics.Clear(new Color(0.051f, 0.149f, 0.271f, 1.0f));
            if (GameMain.DebugDraw)
            {
                GUI.DrawLine(spriteBatch, new Vector2(Submarine.MainSub.HiddenSubPosition.X, -cam.WorldView.Y), new Vector2(Submarine.MainSub.HiddenSubPosition.X, -(cam.WorldView.Y - cam.WorldView.Height)), Color.White * 0.5f, 1.0f, (int)(2.0f / cam.Zoom));
                GUI.DrawLine(spriteBatch, new Vector2(cam.WorldView.X, -Submarine.MainSub.HiddenSubPosition.Y), new Vector2(cam.WorldView.Right, -Submarine.MainSub.HiddenSubPosition.Y), Color.White * 0.5f, 1.0f, (int)(2.0f / cam.Zoom));
            }
           
            Submarine.Draw(spriteBatch, true);

            if (!characterMode && !wiringMode)
            {
                if (MapEntityPrefab.Selected != null) MapEntityPrefab.Selected.DrawPlacing(spriteBatch,cam);
                MapEntity.DrawSelecting(spriteBatch, cam);
            }
            spriteBatch.End();

            if (GameMain.LightManager.LightingEnabled && lightingEnabled)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, Lights.CustomBlendStates.Multiplicative, null, DepthStencilState.None, null, null, null);
                spriteBatch.Draw(GameMain.LightManager.LightMap, new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight), Color.White);
                spriteBatch.End();
            }

            //-------------------- HUD -----------------------------

            spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, GameMain.ScissorTestEnable);

            if (Submarine.MainSub != null)
            {
                GUI.DrawIndicator(
                    spriteBatch, Submarine.MainSub.WorldPosition, cam,
                    cam.WorldView.Width,
                    GUI.SubmarineIcon, Color.LightBlue * 0.5f);
            }
            
            leftPanel.DrawManually(spriteBatch);
            topPanel.DrawManually(spriteBatch);
            
            if ((characterMode || wiringMode) && dummyCharacter != null)                     
            {
                dummyCharacter.DrawHUD(spriteBatch, cam, false);
                
                if (wiringMode) wiringToolPanel.DrawManually(spriteBatch);
            }
            else
            {
                if (loadFrame != null)
                {
                    loadFrame.DrawManually(spriteBatch);
                }
                else if (saveFrame != null)
                {
                    saveFrame.DrawManually(spriteBatch);
                }
                else if (entityMenuOpen)
                {
                    entityMenu.DrawManually(spriteBatch);
                }

                MapEntity.DrawEditor(spriteBatch, cam);
            }

            if (tutorial != null) tutorial.Draw(spriteBatch);

            GUI.Draw((float)deltaTime, spriteBatch, cam);

            if (!PlayerInput.LeftButtonHeld()) Inventory.draggingItem = null;
                                              
            spriteBatch.End();
        }

        private void CreateImage(int width, int height, Stream stream)
        {
            MapEntity.SelectedList.Clear();

            RenderTarget2D rt = new RenderTarget2D(
                GameMain.Instance.GraphicsDevice, 
                width, height, false, SurfaceFormat.Color, DepthFormat.None);

            var prevScissorRect = GameMain.Instance.GraphicsDevice.ScissorRectangle;

            Rectangle subDimensions = Submarine.MainSub.CalculateDimensions(false);
            Vector2 viewPos = subDimensions.Center.ToVector2();            
            float scale = Math.Min(width / (float)subDimensions.Width, height / (float)subDimensions.Height);

            var viewMatrix = Matrix.CreateTranslation(new Vector3(width / 2.0f, height / 2.0f, 0));
            var transform = Matrix.CreateTranslation(
                new Vector3(-viewPos.X, viewPos.Y, 0)) *
                Matrix.CreateScale(new Vector3(scale, scale, 1)) *
                viewMatrix;

            GameMain.Instance.GraphicsDevice.SetRenderTarget(rt);
            SpriteBatch spriteBatch = new SpriteBatch(GameMain.Instance.GraphicsDevice);

            Sprite backgroundSprite = LevelGenerationParams.LevelParams.Find(l => l.BackgroundTopSprite != null).BackgroundTopSprite;
            if (backgroundSprite != null)
            {
                spriteBatch.Begin();
                backgroundSprite.Draw(spriteBatch, Vector2.Zero, new Color(0.025f, 0.075f, 0.131f, 1.0f));
                spriteBatch.End();
            }
            
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, null, null, null, null, transform);            
            Submarine.Draw(spriteBatch, false);
            Submarine.DrawFront(spriteBatch);
            Submarine.DrawDamageable(spriteBatch, null);            
            spriteBatch.End();

            GameMain.Instance.GraphicsDevice.SetRenderTarget(null);
            rt.SaveAsPng(stream, width, height);            
            rt.Dispose();

            //for some reason setting the rendertarget changes the size of the viewport 
            //but it doesn't change back to default when setting it back to null
            GameMain.Instance.ResetViewPort();
        }

        public void SaveScreenShot(int width, int height, string filePath)
        {
            Stream stream = File.OpenWrite(filePath);
            CreateImage(width, height, stream);
            stream.Dispose();
        }
    }
}
