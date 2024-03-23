using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using UnityEngine;
using MyBox;
using UnityEngine.UI;
using TMPro;
using HarmonyLib;

namespace EZDelivery
{
    
    public class EZDelivery : MelonMod
    {
        private KeyCode keyBind;

        // Preferences
        private MelonPreferences_Category _preferencesCategory;

        private string _keyBindEntry;
        private bool _rackFreeSlots = false;

        // UI Values
        private bool ui__displayUI = false;
        private int ui__displayCount = 0;
        private GameObject ui__display;
        private Sprite ui__productIcon;
        private bool ui__restocking = false;

        public override void OnInitializeMelon()
        {

            var harmony = new HarmonyLib.Harmony("EZDelivery");
            harmony.PatchAll();

            harmony.GetPatchedMethods().ForEach(method =>
            {
                Debug.Log(method.Name);
            });

            // Create Preference Category
            _preferencesCategory = MelonPreferences.CreateCategory("EZDelivery");
            MelonPreferences_Entry _keyBindEntryPref;
            _keyBindEntryPref = _preferencesCategory.CreateEntry<string>("KeyBind", "L");
            MelonPreferences_Entry _rackFreeSlotsPref;
            _rackFreeSlotsPref = _preferencesCategory.CreateEntry<bool>("RackFreeSlots", false);
            
            // Get value retrieved from config
            _keyBindEntry = MelonPreferences.GetEntry("EZDelivery", "KeyBind").GetValueAsString();

            bool parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "RackFreeSlots").BoxedValue.ToString(), out _rackFreeSlots);

            if (!parsed)
            {
                MelonLogger.Error("There was a problem parsing the RackFreeSlots field from the config, it is an invalid boolean value. Falling back to false");
                _rackFreeSlots = false;
            }

            keyBind = KeyCode.None;
            parsed = KeyCode.TryParse(_keyBindEntry, out keyBind);
            if (!parsed)
            {
                MelonLogger.Error(String.Format("There was a problem parsing the KeyBind from the config, {0} is an invalid keybind. Falling back to L", _keyBindEntry));
                keyBind = KeyCode.L; 
            }

            MelonPreferences.Save();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            base.OnSceneWasInitialized(buildIndex, sceneName);
            CreateUI();
        }

        public override void OnUpdate()
        {
            // Update UI values
            ui__displayUI = MemoryValues.shouldDisplay;
            ui__displayCount = MemoryValues.count;
            ui__productIcon = MemoryValues.productIcon;
            ui__restocking = MemoryValues.restocking;

            Debug.Log(MemoryValues.shouldDisplay.ToString());

            if (Input.GetKeyDown(keyBind))
            {
                GameObject player = GameObject.Find("Player");

                if (player != null)
                {
                    BoxInteraction bi = player.GetComponent<BoxInteraction>();
                    
                    if (bi != null)
                    {

                        if (bi.Interactable is Box && bi.enabled) // BoxInteraction MUST be enabled, else magic invisible boxes fill the racks
                        {
                            PlaceBoxInRack(player, bi);
                        }
                    }
                }
            }

            // Do UI
            ui__display.SetActive(ui__displayUI);
            RenderUI();
        }

        

        private void CreateUI()
        {
            // dupe from the expenses hint UI
            GameObject currentCanvas = GameObject.Find("Dynamic Prices Canvas");
            GameObject canvas = UnityEngine.Object.Instantiate(currentCanvas);
            canvas.transform.SetParent(GameObject.Find("---UI---").transform);
            canvas.name = "Rack Info Canvas";

            GameObject window = canvas.transform.Find("Price Change Notification Window").gameObject;
            window.name = "Notification Window";
            ui__display = window;

            // Title Section

            GameObject title = window.transform.Find("Title").gameObject;
            
            GameObject titleText = title.transform.Find("Title Text").gameObject;
            GameObject productIcon = title.transform.Find("Notification Icon").gameObject;
            productIcon.name = "Product Icon";

            
            TextMeshProUGUI titleTmPro = titleText.GetComponent<TextMeshProUGUI>();
            Debug.Log(titleTmPro.text);

            // This will need to be done again OnRender as the localization module sets the text to the original value AFTER this code has ran
            titleTmPro.text = "Rack Information";
            titleTmPro.fontSize = 18;

            // Content Section

            GameObject content = window.transform.Find("Content").gameObject;
            int count = 0;
            foreach (Transform child in content.transform)
            {
                if (child.gameObject.name == "Dynamic Price Product")
                {
                    count++;
                }
            }

            GameObject productsInRack = content.transform.Find("Dynamic Price Product").gameObject;
            if (count > 1)
            {
                foreach (Transform child in content.transform)
                {
                    if (child == productsInRack.transform)
                        continue;
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            // Products In Rack

            productsInRack.name = "Info-ProductsInRack";
            UnityEngine.GameObject.Destroy(productsInRack.transform.Find("Cost Change Indicator Icon").gameObject);

            GameObject pirContent = productsInRack.transform.Find("Product Name").gameObject;
            pirContent.name = "PIR-Content";
            TextMeshProUGUI pirTmPro = pirContent.GetComponent<TextMeshProUGUI>();
            pirTmPro.text = "Products in rack: " + ui__displayCount.ToString();

            // Restocking

            GameObject restocking = UnityEngine.Object.Instantiate(productsInRack);
            restocking.name = "Info-Restocking";
            restocking.transform.SetParent(content.transform);
            GameObject restockingContent = restocking.transform.Find("PIR-Content").gameObject;
            restockingContent.name = "Restocking-Content";
            TextMeshProUGUI resTmPro = restockingContent.GetComponent<TextMeshProUGUI>();
            resTmPro.text = "Restocking?: No";

            window.transform.Find("Subnote").gameObject.SetActive(false);

            window.SetActive(false);
        }

        private void RenderUI()
        {
            GameObject window = ui__display;
            GameObject title = window.transform.Find("Title").gameObject;
            GameObject content = window.transform.Find("Content").gameObject;
            if (window.activeSelf)
            {
                if (ui__productIcon != null)
                {
                    GameObject productIcon = title.transform.Find("Product Icon").gameObject;
                    Image image = productIcon.GetComponent<Image>();
                    image.sprite = ui__productIcon;
                }

                TextMeshProUGUI titleTmPro = title.transform.Find("Title Text").gameObject.GetComponent<TextMeshProUGUI>();
                titleTmPro.text = "Rack Information";
                titleTmPro.fontSize = 18;

                GameObject productsInRackContent = content.transform.Find("Info-ProductsInRack").gameObject.transform.Find("PIR-Content").gameObject;
                TextMeshProUGUI pirTmPro = productsInRackContent.GetComponent<TextMeshProUGUI>();
                pirTmPro.text = "Products in rack: " + ui__displayCount.ToString();

                GameObject restockingContent = content.transform.Find("Info-Restocking").gameObject.transform.Find("Restocking-Content").gameObject;
                TextMeshProUGUI resTmPro = restockingContent.GetComponent<TextMeshProUGUI>();
                if (ui__restocking)
                    resTmPro.text = "Restocking?: Yes";
                else
                    resTmPro.text = "Restocking?: No";
            }
        }


        private void PlaceBoxInRack(GameObject player, BoxInteraction boxInteraction)
        {
            Box box = (Box)boxInteraction.Interactable;
            RackManager rackManager = Singleton<RackManager>.Instance;
            if (rackManager == null)
                return;

            ProductSO product = box.Product;
            
            EmployeeManager employeeManager = Singleton<EmployeeManager>.Instance;
            
            if (employeeManager.IsProductOccupied(product.ID))
            {
                CustomWarning("Occupied by Restocker");
                return;
            }

            RackSlot rackSlot = rackManager.GetRackSlotThatHasSpaceFor(product.ID, box.BoxID);

            // shouldn't happen but if it does:
            if (rackSlot == null)
            {
                CustomWarning("No rack space");
                return;
            }
                
            // if there is a matching label OR if items can be racked on free space
            if (rackSlot.Data.ProductID == product.ID || _rackFreeSlots)
            {
                box.CloseBox();
                rackSlot.AddBox(box.BoxID, box);
                box.Racked = true;

                Singleton<PlayerObjectHolder>.Instance.PlaceBoxToRack();
                Singleton<PlayerInteraction>.Instance.InteractionEnd(boxInteraction);
            } else 
            {
                Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, null);
            }
            
            
        }

        private void CustomWarning(string text)
        {
            Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, null);
            GameObject warningCanvas = GameObject.Find("Warning Canvas");
            GameObject title = warningCanvas.transform.Find("Interaction Warning").transform.Find("BG").transform.Find("Title").gameObject;
            TextMeshProUGUI tmProUGUI = title.GetComponent<TextMeshProUGUI>();
            tmProUGUI.text = "<sprite=0> " + text;

        }
    }

    public static class MemoryValues
    {
        public static int count = 0;
        public static bool shouldDisplay = false;
        public static Sprite productIcon = null;
        public static bool restocking = false;
    }

    [HarmonyPatch(typeof(BoxInteraction), "OnEnable")]
    public static class BIOnEnablePatch
    {
        
        static void Postfix(BoxInteraction __instance)
        {
            
            BoxInteraction boxInteraction = __instance;
            if (boxInteraction.Interactable is Box && __instance.enabled)
            {
                RackManager rackManager = Singleton<RackManager>.Instance;
                Box box = (Box)boxInteraction.Interactable;
                int finalCount = 0;
                rackManager.Data.ForEach(rackData =>
                {
                    List<RackSlotData> slotData = rackData.RackSlots;
                    slotData.ForEach(rackSlot =>
                    {
                        
                        if (rackSlot.ProductID == box.Product.ID)
                        {
                            
                            List<BoxData> boxDataList = rackSlot.RackedBoxDatas;
                            boxDataList.ForEach(boxData =>
                            {
                                finalCount += boxData.ProductCount;
                            });
                        }

                    });
                });

                MemoryValues.count = finalCount;
                MemoryValues.shouldDisplay = true;
                MemoryValues.productIcon = box.Product.ProductIcon;
                MemoryValues.restocking = Singleton<EmployeeManager>.Instance.IsProductOccupied(box.Product.ID);
                MelonLogger.Msg("Final Count: " + finalCount.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(BoxInteraction), "OnDisable")]
    public static class BIOnDisbalePatch
    {

        static void Postfix(BoxInteraction __instance)
        {
            MemoryValues.shouldDisplay = false; 
        }
    }


}





  
