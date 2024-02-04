import {
  addProjectConfiguration,
  formatFiles,
  generateFiles,
  getWorkspaceLayout,
  output,
  Tree,
} from "@nx/devkit"
import { ProjectGeneratorSchema } from "./schema"
import * as path from "path"
import { getUnityBasePath } from "../../utils/platform"
import { promptForUnityVersion } from "../../utils/prompts"
import * as fs from "fs"
import * as os from "os"
import axios from "axios"
import AdmZip from "adm-zip"
import { addDependencyToUnityProject, createUnityProject } from "../../utils/unity-project"
import { installPackage } from "../../utils/package-manager"

export async function projectGenerator(tree: Tree, options: ProjectGeneratorSchema) {
  const { name: projectName } = options
  const projectRoot = path.join(getWorkspaceLayout(tree).appsDir, projectName)

  // Add the project to the Nx workspace
  addProjectConfiguration(tree, options.name, {
    root: projectRoot,
    projectType: "application",
    sourceRoot: `${projectRoot}/Assets`,
    targets: {
      build: {
        executor: "nx-unity:build",
        configurations: {
          windows: {
            executeMethod: "BuildCommands.BuildWindows",
          },
          macos: {
            executeMethod: "BuildCommands.BuildMacOS",
          },
          linux: {
            executeMethod: "BuildCommands.BuildLinux",
          },
          android: {
            executeMethod: "BuildCommands.BuildAndroid",
          },
          ios: {
            executeMethod: "BuildCommands.BuildiOS",
          },
          webgl: {
            executeMethod: "BuildCommands.BuildWebGL",
          },
        },
        defaultConfiguration: "windows",
      },
    },
  })

  // Check if Unity is installed
  const unityBasePath = getUnityBasePath()
  if (fs.existsSync(unityBasePath) === false) {
    throw new Error(`Unity installation not found at ${unityBasePath}`)
  }

  // Let the user select the Unity version
  const unityVersion = await promptForUnityVersion(unityBasePath, "Select Unity version")

  // Copy general starter files
  generateFiles(tree, path.join(__dirname, "files"), projectRoot, options)

  // Download and extract the selected template
  const basePath = `${os.tmpdir()}/nx-unity/templates/${unityVersion}`
  const templatePath = `${basePath}/${options.template}`
  const templateZipPath = `${templatePath}.zip`
  const downloadUrl = `https://nx-unity-cdn.vercel.app/templates/${unityVersion}/${options.template}.zip`
  try {
    fs.mkdirSync(templatePath, { recursive: true })
    await downloadFile(downloadUrl, templateZipPath)
    unzipFile(templateZipPath, templatePath)
    generateFiles(tree, templatePath, projectRoot, options)
  } catch (e) {
    if (options.template === "builtin") {
      output.warn({
        title: "Creating project from scratch",
        bodyLines: [
          `Failed to download template from ${downloadUrl}`,
          "Creating a new project from scratch instead",
        ],
      })
      await createUnityProject(unityBasePath, unityVersion, projectRoot)
    } else {
      throw new Error(`Failed to download template from ${downloadUrl}`)
    }
  }

  // Add NxUnity package
  addDependencyToUnityProject(
    tree,
    projectName,
    "nx-unity",
    "https://github.com/Glowdragon/nx-unity.git?path=/unity-package"
  )

  // Install OpenUPM
  await installPackage("", "openupm-cli", true)

  await formatFiles(tree)
}

function downloadFile(url: string, outputFilePath: string): Promise<void> {
  return axios({
    method: "GET",
    url: url,
    responseType: "stream",
  }).then((response) => {
    return new Promise<void>((resolve, reject) => {
      const stream = fs.createWriteStream(outputFilePath)
      response.data.pipe(stream).on("finish", resolve).on("error", reject)
    })
  })
}

function unzipFile(zipFilePath: string, outputDir: string): void {
  const zip = new AdmZip(zipFilePath)
  zip.extractAllTo(outputDir, true)
}

export default projectGenerator
