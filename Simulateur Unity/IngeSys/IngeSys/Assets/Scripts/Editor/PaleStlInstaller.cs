using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IngeSys.EditorTools
{
    /// <summary>Installs the newly supplied blade STL in the loaded karst scene, preserving source assets.</summary>
    [InitializeOnLoad]
    public static class PaleStlInstaller
    {
        const string ModelPath = "Assets/CAD/Robot/Derived/pale.obj";
        const string ScenePath = "Assets/Scenes/RobotFishKatarstique.unity";

        static PaleStlInstaller()
        {
            EditorApplication.delayCall += InstallIfReady;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode) { InstallIfReady(); }

        [MenuItem("IngeSys/Install supplied pale STL")]
        public static void InstallIfReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var robot = GameObject.Find("RobotFish");
            if (!source || !robot) return;
            var pivot = robot.transform.Find("Pale_CAD_Pivot");
            if (!pivot || pivot.Find("Pale_STL")) return;
            bool previouslyDirty = scene.isDirty;
            var blade = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            Undo.RegisterCreatedObjectUndo(blade, "Install supplied pale STL");
            blade.name = "Pale_STL";
            blade.transform.SetParent(pivot, false);
            blade.transform.localPosition = new Vector3(0, 0, -.34f);
            blade.transform.localRotation = Quaternion.Euler(0, 180, 0);
            blade.transform.localScale = new Vector3(1.5f, 1, 1);
            var parameters = robot.GetComponent<IngeSys.Robot.RobotFishParameters>();
            var material = parameters.fin.GetComponent<Renderer>().sharedMaterial;
            foreach (var renderer in blade.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
            // Align the four 3mm blade holes to the support's 16mm pitch mounting row.
            var support = pivot.Find("support_pale_V2");
            if (support)
            {
                Undo.RecordObject(support, "Align CAD pale support");
                support.localRotation = Quaternion.AngleAxis(180, Vector3.up) * Quaternion.Euler(-90, 0, 0);
                support.localPosition = new Vector3(0, .075f, -.068571429f);
            }
            if (parameters.fin) parameters.fin.GetComponent<Renderer>().enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            // Save only when there were no outstanding user edits before installation.
            if (!previouslyDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log("pale.stl installed at CAD servo pivot: 300 x 150 x 1.5 mm. Check rear view and save scene if dirty.");
        }
    }
}
