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
    /// <summary>
    /// EZDelivery mod for Supermarket Simulator
    /// Version: 1.5.6
    /// Created by WaffDev
    /// </summary>
    public class EZDelivery : MelonMod
    {
        /* Preferences */

        // The keybind used to rack a box in your hand
        private KeyCode rackKeybind;
        // The MelonPreferences instance
        private MelonPreferences_Category melonPrefCategory;

        /* UI Values */

        // Should the UI be displayed?
        private bool displayUI = false;
        // The count of how many products are in storage of the current item
        private int uiDisplayCount = 0;
        // The physical GameObject of the UI
        private GameObject uiDisplayGameObject;
        // The icon used in the UI display
        private Sprite uiProductIcon;
        // Is the product in the UI currently being restocked?
        private bool uiIsRestocking = false;

        /// <summary>
        /// Called when the mod is initialised
        /// All set up and configuration is handled here
        /// </summary>
        public override void OnInitializeMelon()
        {
            // Set up harmony and patch methods
            var harmony = new HarmonyLib.Harmony("EZDelivery");
            harmony.PatchAll(typeof(EZDelivery));

            harmony.GetPatchedMethods().ForEach(method =>
            {
                Debug.Log("Patched method: " + method.Name);
            });

            // Create preferences entries
            melonPrefCategory = MelonPreferences.CreateCategory("EZDelivery");
            MelonPreferences_Entry prefKeybindEntry = melonPrefCategory.CreateEntry<string>("KeyBind", "L");
            MelonPreferences_Entry prefRackFreeSlots = melonPrefCategory.CreateEntry<bool>("RackFreeSlots", false);
            MelonPreferences_Entry prefAutoRack = melonPrefCategory.CreateEntry<bool>("AutoRack", false);
            MelonPreferences_Entry prefIsUIEnabled = melonPrefCategory.CreateEntry<bool>("EnableUI", true);

            /* Retrieve and Assign Values */
            // Key bind
            string keyBindEntry = MelonPreferences.GetEntry("EZDelivery", "KeyBind").GetValueAsString();
            rackKeybind = KeyCode.None;
            bool parsed = KeyCode.TryParse(prefKeybindEntry.GetValueAsString(), out rackKeybind);
            if (!parsed) // Throw error and fall back to default if invalid
            {
                MelonLogger.Error(string.Format("There was a problem parsing the KeyBind from the config, {0} is an invalid keybind. Falling back to L", prefKeybindEntry.GetValueAsString()));
                rackKeybind = KeyCode.L;
            }
            // Auto Rack
            parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "AutoRack").BoxedValue.ToString(), out CrossoverClass.autoRack);
            if (!parsed) 
            { 
                MelonLogger.Error("There was a problem parsing the AutoRack field from the config, it is an invalid boolean value. Falling back to false");
                CrossoverClass.autoRack = false;
            }
            // Rack Free Slots
            parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "RackFreeSlots").BoxedValue.ToString(), out CrossoverClass.rackFreeSlots);
            if (!parsed)
            {
                MelonLogger.Error("There was a problem parsing the RackFreeSlots field from the config, it is an invalid boolean value. Falling back to false");
                CrossoverClass.rackFreeSlots = false;
            }
            // UI Enabled
            parsed = bool.TryParse(MelonPreferences.GetEntry("EZDelivery", "EnableUI").BoxedValue.ToString(), out CrossoverClass.uiEnabled);
            if (!parsed)
            {
                MelonLogger.Error("There was a problem parsing the EnableUI field from the config, it is an invalid boolean value. Falling back to false");
                CrossoverClass.uiEnabled = false;
            }

            MelonPreferences.Save();

        }

        /// <summary>
        /// Overrides the scene initialize event to load the UI
        /// </summary>
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            base.OnSceneWasInitialized(buildIndex, sceneName);
            if (sceneName == "Main Scene") // Check we are in the right scene and not on the menu
            {
                if (CrossoverClass.uiEnabled)
                    CreateUI();
            }

        }


        /// <summary>
        /// Overrides the update event for game logic
        /// Listens for key binds and updates the UI
        /// </summary>
        public override void OnUpdate()
        {
            // Update UI values
            displayUI = CrossoverClass.shouldDisplay;
            uiDisplayCount = CrossoverClass.count;
            uiProductIcon = CrossoverClass.productIcon;
            uiIsRestocking = CrossoverClass.restocking;
            
            if (Input.GetKeyDown(rackKeybind)) // If the rack keybind has been pressed
            {
                GameObject player = GameObject.Find("Player");
                if (player != null) // Ensure the player is loaded
                {
                    BoxInteraction bi = player.GetComponent<BoxInteraction>();
                    if (bi != null) // Check the player is holding a box
                    {
                        if (bi.Interactable is Box && bi.enabled) // BoxInteraction MUST be enabled, else magic invisible boxes fill the racks
                        {
                            PlaceBoxInRack(player, bi);
                        }
                    }
                }
            }

            // Draw the UI
            if (CrossoverClass.uiEnabled)
            {
                if (uiDisplayGameObject != null)
                {
                    // Enable UI game object and render
                    uiDisplayGameObject.SetActive(displayUI);

                    RenderUI();
                }
            }
        }

        /// <summary>
        /// Creates the UI based on reusable game objects
        /// **very unstable and easily breakable way to do it, i'll rewrite this at some point**
        /// </summary>
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
            uiDisplayGameObject = window;

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
            pirTmPro.text = "Products in rack: " + uiDisplayCount.ToString();


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

        /// <summary>
        /// Renders the UI
        /// Meant to be called on update
        /// </summary>
        private void RenderUI()
        {
            GameObject window = uiDisplayGameObject;
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

                    // Set the correct text for the UI 
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

        /*
         * Fix provided by rj1244 on GitHub for EZD Patch 1.5.6
         * in pull request #3
         */
        /// <summary>
        /// Places a box in the storage
        /// </summary>
        /// <param name="player">The instance of the player GameObject</param>
        /// <param name="boxInteraction">The box to place</param>
        private void PlaceBoxInRack(GameObject player, BoxInteraction boxInteraction)
        {
            // Retrieve the interactable from the BI and cast it to a box
            Box box = (Box)boxInteraction.Interactable;
            RackManager rackManagerInstance = Singleton<RackManager>.Instance;
            if (rackManagerInstance == null) // Check if instance exists
                return;
            ProductSO product = box.Product;
            if (Singleton<EmployeeManager>.Instance.IsProductOccupied(product.ID)) // Check if the box is occupied by a restocker. If so it'll break the restocker
            {
                CrossoverClass.CustomWarning("Occupied by Restocker");
            }
            else
            {
                Restocker restocker = new Restocker();
                RestockerManagementData data = new RestockerManagementData();
                data.UseUnlabeledRacks = CrossoverClass.rackFreeSlots;
                restocker.SetRestockerManagementData(data);
                // Get the rack slot based on the rack manager
                RackSlot slotThatHasSpaceFor = rackManagerInstance.GetRackSlotThatHasSpaceFor(product.ID, box.BoxID, restocker);
                Debug.Log("Has Product? " + slotThatHasSpaceFor.HasProduct);
                Debug.Log("Rack Free Slots? " + CrossoverClass.rackFreeSlots);
                if (slotThatHasSpaceFor == null || !slotThatHasSpaceFor.HasLabel && !CrossoverClass.rackFreeSlots) // Check if there is no rack space
                {
                    CrossoverClass.CustomWarning("No rack space");
                }
                else if (slotThatHasSpaceFor.Data.ProductID == product.ID || CrossoverClass.rackFreeSlots)
                {
                    box.CloseBox();
                    slotThatHasSpaceFor.AddBox(box.BoxID, box);
                    box.Racked = true;
                    Traverse.Create((object)boxInteraction).Field("m_Box").SetValue((object)null);
                    Singleton<PlayerObjectHolder>.Instance.PlaceBoxToRack();
                    Singleton<PlayerInteraction>.Instance.InteractionEnd((Interaction)boxInteraction);
                }
                else
                    Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, (string[])null);
            }
        }


    }

    public static class CrossoverClass
    {
        public static int count;
        public static bool shouldDisplay;
        public static Sprite productIcon;
        public static bool restocking;
        public static bool autoRack;
        public static bool rackFreeSlots;
        public static bool uiEnabled;

        public static void CustomWarning(string text)
        {
            Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, (string[])null);
            GameObject.Find("Warning Canvas").transform.Find("Interaction Warning").transform.Find("BG").transform.Find("Title").gameObject.GetComponent<TextMeshProUGUI>().text = "<sprite=0> " + text;
        }

        public static void AutoRack()
        {
            List<Box> boxList = new List<Box>();
            StorageStreet storageStreetManager = Singleton<StorageStreet>.Instance;
            storageStreetManager.GetAllBoxesFromStreet().ToList().ForEach((box) =>
            {
                if (box.ProductCount > 0)
                {
                    storageStreetManager.boxes.Remove(box);
                    boxList.Add(box);
                }
            });
           
            boxList.ForEach(box =>
            {
                Restocker restocker = new Restocker();
                RestockerManagementData data = new RestockerManagementData();
                data.UseUnlabeledRacks = CrossoverClass.rackFreeSlots;
                restocker.SetRestockerManagementData(data);
                RackSlot slotThatHasSpaceFor = Singleton<RackManager>.Instance.GetRackSlotThatHasSpaceFor(box.Product.ID, box.BoxID, restocker);
                if (slotThatHasSpaceFor == null)
                    return;
                if (slotThatHasSpaceFor.HasLabel && !CrossoverClass.rackFreeSlots || 
                    !slotThatHasSpaceFor.HasLabel && CrossoverClass.rackFreeSlots)
                {
                    foreach (Collider componentsInChild in box.gameObject.GetComponentsInChildren<Collider>())
                        componentsInChild.isTrigger = false;
                    Rigidbody component;
                    if (box.TryGetComponent<Rigidbody>(out component))
                    {
                        component.isKinematic = true;
                        component.velocity = Vector3.zero;
                        component.interpolation = RigidbodyInterpolation.None;
                    }
                    slotThatHasSpaceFor.AddBox(box.BoxID, box);
                    slotThatHasSpaceFor.EnableBoxColliders = true;
                    int layer = LayerMask.NameToLayer("Interactable");
                    box.gameObject.layer = layer;
                    box.Racked = true;
                }
                else if (!CrossoverClass.rackFreeSlots)
                    CrossoverClass.CustomWarning("Some products had no space");
            });
        }
    }


    [HarmonyPatch(typeof(BoxInteraction), "OnEnable")]
    public static class BIOnEnablePatch
    {
        private static void Postfix(BoxInteraction __instance)
        {
            BoxInteraction boxInteraction = __instance;
            if (!(boxInteraction.Interactable is Box) || !__instance.enabled)
                return;
            Box interactable = (Box)boxInteraction.Interactable;
            if (interactable.gameObject.name != "SpecialEMarketBox")
            {
                RackManager instance = Singleton<RackManager>.Instance;
                int finalCount = 0;
                Dictionary<int, List<RackSlot>> dictionary = (Dictionary<int, List<RackSlot>>)Traverse.Create((object)instance).Field("m_RackSlots").GetValue();
                if (dictionary != null)
                {
                    List<RackSlot> rackSlotList;
                    if (dictionary.TryGetValue(interactable.Product.ID, out rackSlotList))
                        rackSlotList.ForEach((Action<RackSlot>)(rackSlot =>
                        {
                            if (!rackSlot.HasProduct)
                                return;
                            finalCount += rackSlot.Data.TotalProductCount;
                        }));
                }
                else
                    MelonLogger.Error("There was an issue attempting to get all rack slots");
                CrossoverClass.count = finalCount;
                CrossoverClass.shouldDisplay = true;
                CrossoverClass.productIcon = interactable.Product.ProductIcon;
                CrossoverClass.restocking = Singleton<EmployeeManager>.Instance.IsProductOccupied(interactable.Product.ID);
                MelonLogger.Msg("Final Count: " + finalCount.ToString());
                CrossoverClass.shouldDisplay = true;
            }
        }
    }

    [HarmonyPatch(typeof(BoxInteraction), "OnDisable")]
    public static class BIOnDisablePatch
    {
        private static void Postfix(BoxInteraction __instance)
        {
            CrossoverClass.shouldDisplay = false;
        }
    }

    [HarmonyPatch(typeof(DeliveryManager), "Delivery")]
    public static class DeliveryManagerPatch
    {
        private static void Postfix(DeliveryManager __instance)
        {
            if (!CrossoverClass.autoRack)
                return;
            GameObject gameObject = __instance.gameObject;
            CrossoverClass.AutoRack();
            CrossoverClass.CustomWarning("Called Delivery hook!");
        }
    }
}





  
