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
using System.Reflection;

namespace EZDelivery
{

    public class EZDelivery : MelonMod
    {
        private KeyCode keyBind;

        // Preferences
        private MelonPreferences_Category _preferencesCategory;

        private string _keyBindEntry;


        // UI Values
        private bool ui__displayUI = false;
        private int ui__displayCount = 0;
        private GameObject ui__display;
        private Sprite ui__productIcon;
        private bool ui__restocking = false;

        public override void OnInitializeMelon()
        {

            var harmony = new HarmonyLib.Harmony("EZDelivery");
            harmony.PatchAll(typeof(EZDelivery));

            harmony.GetPatchedMethods().ForEach(method =>
            {
                Debug.Log("Patched method: " + method.Name);
            });

            // Create Preference Category
            _preferencesCategory = MelonPreferences.CreateCategory("EZDelivery");
            MelonPreferences_Entry _keyBindEntryPref;
            _keyBindEntryPref = _preferencesCategory.CreateEntry<string>("KeyBind", "L");
            MelonPreferences_Entry _rackFreeSlotsPref;
            _rackFreeSlotsPref = _preferencesCategory.CreateEntry<bool>("RackFreeSlots", false);
            MelonPreferences_Entry _autoRackPref;
            _autoRackPref = _preferencesCategory.CreateEntry<bool>("AutoRack", false);
            MelonPreferences_Entry _uiEnabledPref;
            _uiEnabledPref = _preferencesCategory.CreateEntry<bool>("EnableUI", true);
            

            // Get value retrieved from config
            _keyBindEntry = MelonPreferences.GetEntry("EZDelivery", "KeyBind").GetValueAsString();
            

            bool parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "AutoRack").BoxedValue.ToString(), out CrossoverClass.autoRack);

            if (!parsed)
            {
                MelonLogger.Error("There was a problem parsing the AutoRack field from the config, it is an invalid boolean value. Falling back to false");
                CrossoverClass.autoRack = false;
            }

            parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "RackFreeSlots").BoxedValue.ToString(), out CrossoverClass.rackFreeSlots);

            if (!parsed)
            {
                MelonLogger.Error("There was a problem parsing the RackFreeSlots field from the config, it is an invalid boolean value. Falling back to false");
                CrossoverClass.rackFreeSlots = false;
            }

            parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "EnableUI").BoxedValue.ToString(), out CrossoverClass.uiEnabled);

            if (!parsed)
            {
                MelonLogger.Error("There was a problem parsing the EnableUI field from the config, it is an invalid boolean value. Falling back to false");
                CrossoverClass.uiEnabled = false;
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
            if (sceneName == "Main Scene")
            {
                if (CrossoverClass.uiEnabled)
                    CreateUI();

            }

        }



        public override void OnUpdate()
        {
            // Update UI values
            ui__displayUI = CrossoverClass.shouldDisplay;
            ui__displayCount = CrossoverClass.count;
            ui__productIcon = CrossoverClass.productIcon;
            ui__restocking = CrossoverClass.restocking;

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

            if (Input.GetKeyDown(KeyCode.Tab)) // force auto rack
            {
                CrossoverClass.AutoRack(Singleton<DeliveryManager>.Instance);
            }

            // Do UI
            if (CrossoverClass.uiEnabled)
            {
                if (ui__display != null)
                {
                    ui__display.SetActive(ui__displayUI);
                    RenderUI();
                }
            }
        }

        private void CreateUI()
        {
            // dupe from the expenses hint UI
            GameObject currentCanvas = GameObject.Find("Dynamic Prices Canvas");
            GameObject canvas = UnityEngine.Object.Instantiate(currentCanvas);
            UnityEngine.Object.Destroy(canvas.GetComponent<PriceChangeNotification>());
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
            UnityEngine.GameObject.Destroy(productsInRack.GetComponent<DynamicPriceProduct>());

            GameObject pirContent = productsInRack.transform.Find("Product Name").gameObject;
            pirContent.name = "PIR-Content";
            TextMeshProUGUI pirTmPro = pirContent.GetComponent<TextMeshProUGUI>();
            pirTmPro.text = "Products in rack: " + ui__displayCount.ToString();


            // Restocking

            GameObject restocking = UnityEngine.Object.Instantiate(productsInRack);
            UnityEngine.GameObject.Destroy(restocking.GetComponent<DynamicPriceProduct>());
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
            if (window != null)
            {
                GameObject title = window.transform.Find("Title").gameObject;
                GameObject content = window.transform.Find("Content").gameObject;
                if (window.activeSelf)
                {
                    if (CrossoverClass.productIcon != null)
                    {
                        GameObject productIcon = title.transform.Find("Product Icon").gameObject;
                        Image image = productIcon.GetComponent<Image>();
                        image.sprite = CrossoverClass.productIcon;
                    }

                    TextMeshProUGUI titleTmPro = title.transform.Find("Title Text").gameObject.GetComponent<TextMeshProUGUI>();
                    titleTmPro.text = "Rack Information";
                    titleTmPro.fontSize = 18;

                    GameObject productsInRackContent = content.transform.Find("Info-ProductsInRack").gameObject.transform.Find("PIR-Content").gameObject;
                    TextMeshProUGUI pirTmPro = productsInRackContent.GetComponent<TextMeshProUGUI>();
                    pirTmPro.text = "Products in rack: " + CrossoverClass.count.ToString();

                    GameObject restockingContent = content.transform.Find("Info-Restocking").gameObject.transform.Find("Restocking-Content").gameObject;
                    TextMeshProUGUI resTmPro = restockingContent.GetComponent<TextMeshProUGUI>();
                    if (CrossoverClass.restocking)
                        resTmPro.text = "Restocking?: Yes";
                    else
                        resTmPro.text = "Restocking?: No";
                }
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
                CrossoverClass.CustomWarning("Occupied by Restocker");
                return;
            }

            RackSlot rackSlot = rackManager.GetRackSlotThatHasSpaceFor(product.ID, box.BoxID);

            // shouldn't happen but if it does:
            if (rackSlot == null)
            {
                CrossoverClass.CustomWarning("No rack space");
                return;
            }

            // if there is a matching label OR if items can be racked on free space
            if (rackSlot.Data.ProductID == product.ID || CrossoverClass.rackFreeSlots)
            {
                box.CloseBox();
                rackSlot.AddBox(box.BoxID, box);
                box.Racked = true;

                HarmonyLib.Traverse.Create(boxInteraction).Field("m_Box").SetValue(null);

                Singleton<PlayerObjectHolder>.Instance.PlaceBoxToRack();
                Singleton<PlayerInteraction>.Instance.InteractionEnd(boxInteraction);
            } else
            {
                Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, null);
            }


        }

      
    }

    public static class CrossoverClass
    {
        public static int count = 0;
        public static bool shouldDisplay = false;
        public static Sprite productIcon = null;
        public static bool restocking = false;
        public static bool autoRack = false;
        public static bool rackFreeSlots = false;
        public static bool uiEnabled = false;

        public static void CustomWarning(string text)
        {
            Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, null);
            GameObject warningCanvas = GameObject.Find("Warning Canvas");
            GameObject title = warningCanvas.transform.Find("Interaction Warning").transform.Find("BG").transform.Find("Title").gameObject;
            TextMeshProUGUI tmProUGUI = title.GetComponent<TextMeshProUGUI>();
            tmProUGUI.text = "<sprite=0> " + text;

        }

        public static void AutoRack(DeliveryManager deliveryManager)
        {
            int count = 0;
            List<Box> boxesToRack = new List<Box>();
            for (int i = 0; i < deliveryManager.transform.childCount; i++)
            {
                count++;
                Transform child = deliveryManager.transform.GetChild(i);
                GameObject boxObject = child.gameObject;
                Box box = null;
                bool isBox = boxObject.TryGetComponent<Box>(out box);

                if (isBox)
                {
                    boxesToRack.Add(box);
                } else
                {
                    FurnitureBox furnitureBox = null;
                    bool isFurniture = boxObject.TryGetComponent<FurnitureBox>(out furnitureBox);
                    if (isFurniture)
                        Debug.Log("Skipped auto-racking a box, the box is furniture");
                }
                
            }

            boxesToRack.ForEach(box =>
            {
                RackManager rackManager = Singleton<RackManager>.Instance;
                RackSlot rackSlot = rackManager.GetRackSlotThatHasSpaceFor(box.Product.ID, box.BoxID);
                if (rackSlot != null)
                {
                    if (box.Product.ID == rackSlot.Data.ProductID || CrossoverClass.rackFreeSlots)
                    {
                        
                        // Fix bug of weird placement, falling through floor etc

                        

                        Collider[] componentsInChildren = box.gameObject.GetComponentsInChildren<Collider>();
                        for (int i = 0; i < componentsInChildren.Length; i++)
                        {
                            componentsInChildren[i].isTrigger = false;
                        }

                        Rigidbody boxPhysics;
                        if (box.TryGetComponent<Rigidbody>(out boxPhysics))
                        {
                            boxPhysics.isKinematic = true;
                            boxPhysics.velocity = Vector3.zero;
                            boxPhysics.interpolation = RigidbodyInterpolation.None;
                        }

                        rackSlot.AddBox(box.BoxID, box);
                        rackSlot.EnableBoxColliders = true;
                        
                        int interactableLayer = LayerMask.NameToLayer("Interactable");
                        box.gameObject.layer = interactableLayer;

                        box.Racked = true;
                        //Singleton<PlayerObjectHolder>.Instance.PlaceBoxToRack();
                        //Singleton<PlayerInteraction>.Instance.InteractionEnd(Singleton<BoxInteraction>.Instance);
                    }
                    else if (!CrossoverClass.rackFreeSlots)
                    {
                        CrossoverClass.CustomWarning("Some products had no space");
                    }
                }
            });

        }
        
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

                
                Dictionary<int, List<RackSlot>> m_RackSlots = (Dictionary<int, List<RackSlot>>)Traverse.Create(rackManager).Field("m_RackSlots").GetValue();
                if (m_RackSlots != null)
                {
                    List<RackSlot> rackSlots;
                    if (m_RackSlots.TryGetValue(box.Product.ID, out rackSlots))
                    {
                        rackSlots.ForEach(rackSlot =>
                        {
                            if (rackSlot.HasProduct)
                            {
                                finalCount += rackSlot.Data.TotalProductCount;
                            }
                        });
                    }
                } else
                {
                    MelonLogger.Error("There was an issue attempting to get all rack slots");
                }

                CrossoverClass.count = finalCount;
                CrossoverClass.shouldDisplay = true;
                CrossoverClass.productIcon = box.Product.ProductIcon;
                CrossoverClass.restocking = Singleton<EmployeeManager>.Instance.IsProductOccupied(box.Product.ID);
                MelonLogger.Msg("Final Count: " + finalCount.ToString());

                CrossoverClass.shouldDisplay = true;
            }
        }
    }

    [HarmonyPatch(typeof(BoxInteraction), "OnDisable")]
    public static class BIOnDisbalePatch
    {

        static void Postfix(BoxInteraction __instance)
        {
            CrossoverClass.shouldDisplay = false;
        }
    }

    
    [HarmonyPatch(typeof(DeliveryManager), "Delivery", new Type[] {typeof(MarketShoppingCart.CartData)})]
    public static class DeliveryManagerPatch
    {
        static void Postfix(DeliveryManager __instance)
        {
            if (CrossoverClass.autoRack)
            {
                GameObject deliveryManager = __instance.gameObject;
                CrossoverClass.AutoRack(__instance);
            }
        }
    }


}





  
