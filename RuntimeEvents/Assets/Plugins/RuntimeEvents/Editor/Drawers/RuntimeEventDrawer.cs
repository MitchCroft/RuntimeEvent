using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace RuntimeEvents {
    /// <summary>
    /// Manage the displaying of the contained persistent data events for Runtime Event
    /// </summary>
    [CustomPropertyDrawer(typeof(RuntimeEventBase), true)]
    public sealed partial class RuntimeEventDrawer : PropertyDrawer {
        /*----------Variables----------*/
        //PRIVATE

        /// <summary>
        /// Store a cache of the generated runtime list elements that need to be displayed
        /// </summary>
        private Dictionary<RuntimeEventBase, ReorderableList> listCache;

        /*----------Functions----------*/
        //PRIVATE

        /// <summary>
        /// Create a reorderable list that will be cached for the displaying of persistent callback elements
        /// </summary>
        /// <param name="eventBase">The object actual that is described by the runtimeEventProp</param>
        /// <param name="runtimeEventProp">The property that contains the base runtime event information that is being used for display</param>
        /// <param name="label">The label that is attached to the property that is to be displayed</param>
        private void CreateCachedList(RuntimeEventBase eventBase, SerializedProperty runtimeEventProp, GUIContent label) {
            //Get the property that stores the persistent callback elements that need to be displayed
            SerializedProperty persistentsProp = runtimeEventProp.FindPropertyRelative("persistents");

            //Create the cache element that will be stored
            ReorderableList list = new ReorderableList(
                runtimeEventProp.serializedObject,
                persistentsProp,
                true,                                                       //Drag-able
                true,                                                       //Header
                true,                                                       //Add Button
                true                                                        //Remove Button
            );

            //Create a label that has the display elements 
            GUIContent displayLabel = new GUIContent(
                (eventBase.DYNAMIC_TYPES.Length > 0 ?
                    label.text + " " + PersistentOptionsUtility.GenerateSignatureLabelString(eventBase.DYNAMIC_TYPES) :
                    label.text
                ),
                label.tooltip
            );

            //Setup the callback functions for calculating and displaying element values
            list.elementHeightCallback += index => PersistentCallbackDrawer.GetElementHeight(persistentsProp.GetArrayElementAtIndex(index));
            list.drawHeaderCallback += rect => EditorGUI.LabelField(rect, displayLabel);
            list.drawElementCallback += (rect, index, active, focused) => {
                //Flag if the height needs to be regenerated
                if (PersistentCallbackDrawer.DrawLayoutElements(
                        rect, 
                        persistentsProp.GetArrayElementAtIndex(index), 
                        eventBase.DYNAMIC_TYPES, 
                        () => {
                            eventBase.DirtyPersistent();
                            runtimeEventProp.serializedObject.Update();
                        }
                    )) {
                    //Persistent events need to be dirtied so that they will be updated if changes occurred
                    eventBase.DirtyPersistent();    

                    //Force this element to need to redraw
                    runtimeEventProp.serializedObject.Update();
                }
            };
            list.onAddCallback = dynamicList => {
                //If this is the first object value, reset the object to its default state 
                if (++dynamicList.serializedProperty.arraySize == 1) {
                    //Apply the new values to the object
                    runtimeEventProp.serializedObject.ApplyModifiedProperties();

                    //Get the new property element in the array
                    SerializedProperty firstProp = persistentsProp.GetArrayElementAtIndex(0);

                    //Retrieve all of the objects at this point
                    PersistentCallback[] firsts;
                    firstProp.GetPropertyValues(out firsts);

                    //Reset the values of this object to the default values
                    for (int i = 0; i < firsts.Length; i++) {
                        if (firsts[i] != null)
                            firsts[i].ResetAll();
                    }
                }
            };

            //Add this option to the cache
            listCache[eventBase] = list;
        }

        //PUBLIC

        /// <summary>
        /// Initialise this object with default values
        /// </summary>
        public RuntimeEventDrawer() { listCache = new Dictionary<RuntimeEventBase, ReorderableList>(); }

        /// <summary>
        /// Retrieve the height of the area within the inspector that this property will take up
        /// </summary>
        /// <param name="property">The property that is to be displayed within the inspector</param>
        /// <param name="label">The label that has been assigned to the property</param>
        /// <returns>Returns the required height of the inspector window for this property in pixels</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            //Get the target object of the property
            RuntimeEventBase eventBase;
            property.GetPropertyValue(out eventBase);

            //Check if there is an entry for this object
            if (!listCache.ContainsKey(eventBase))
                CreateCachedList(eventBase, property, label); //Setup the events list

            //Grab the cache object being checked
            ReorderableList list = listCache[eventBase];

            //Return the cached height value
            return list.GetHeight();
        }

        /// <summary>
        /// Display the elements of the property within the designated area on the inspector area
        /// </summary>
        /// <param name="position">The position within the inspector that the property should be drawn to</param>
        /// <param name="property">The property that is to be displayed within the inspector</param>
        /// <param name="label">The label that has been assigned to the property</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            //Get the target object of the property
            RuntimeEventBase eventBase;
            property.GetPropertyValue(out eventBase);

            //Check there is a cache for this value
            if (!listCache.ContainsKey(eventBase))
                throw new NullReferenceException("No cached list is present for the current Event Base object. Can't draw inspector values");

            //Begin checking for object changes 
            Undo.RecordObjects(property.serializedObject.targetObjects, "Modify Runtime Event");

            //Display the persistent callback options
            listCache[eventBase].DoList(position);
        }
    }
}