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

namespace EZDelivery
{
    
    public class EZDelivery : MelonMod
    {
        private KeyCode keyBind;

        // Preferences
        private MelonPreferences_Category _preferencesCategory;

        private string _keyBindEntry;

        public override void OnInitializeMelon()
        {
            // Create Preference Category
            _preferencesCategory = MelonPreferences.CreateCategory("EZDelivery");
            MelonPreferences_Entry _keyBindEntryPref;
            _keyBindEntryPref = _preferencesCategory.CreateEntry<string>("KeyBind", "L");
            // Get value retrieved from config
            _keyBindEntry = MelonPreferences.GetEntry("EZDelivery", "KeyBind").GetValueAsString();

            keyBind = KeyCode.None;
            bool parsed = KeyCode.TryParse(_keyBindEntry, out keyBind);
            if (!parsed)
            {
                MelonLogger.Error(String.Format("There was a problem parsing the KeyBind from the config, {0} is an invalid keybind. Falling back to L", _keyBindEntry));
                keyBind = KeyCode.L; 
            }

            MelonPreferences.Save();
        }

        public override void OnUpdate()
        {

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
            if (rackSlot.Data.ProductID == product.ID)
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

    
}



  
