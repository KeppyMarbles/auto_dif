import threading
import socket
import datetime
import traceback
import bpy
import os
import shutil
import tempfile


bl_info = {
    "name": "Auto DIF",
    "author": "Keppy",
    "version": (1, 0, 0),
    "description": "Connects with Marble Blast to enable hot reloading of DIFs",
    "warning": "",
    "doc_url": "",
    "category": "Import-Export"
}


class AutoDIFPreferences(bpy.types.AddonPreferences):
    bl_idname = __name__

    game_dir: bpy.props.StringProperty(
        name="Game Directory",
        subtype="DIR_PATH",
        default="",
    )

    export_on_save: bpy.props.BoolProperty(
        name="Export On Save",
        default=True,
    )

    bspmode: bpy.props.EnumProperty(
        items=[
            ("Fast", "Fast", ""),
            ("Exhaustive", "Exhaustive", ""),
            ("None", "None", "")
        ],
        name="BSP Algorithm",
        description="The algorithm used for building the BSP Tree of the DIF.",
        default="None"
    )

    def draw(self, context):
        layout = self.layout
        layout.prop(self, "game_dir")
        sublayout = layout.row()
        sublayout.prop(self, "export_on_save")
        sublayout = layout.row()
        sublayout.prop(self, "bspmode")
        

class EXPORT_OT_manual(bpy.types.Operator):
    bl_idname = "export_scene.tomb"
    bl_label = "Export to Marble Blast"

    def execute(self, context):
        if(server.current_conn):
            try:
                server.export_difs()
            except Exception as e:
                self.report({"ERROR"}, f"Export failed: {e}")
                return {"CANCELLED"}
        else:
            self.report({"ERROR"}, f"Export failed: No game connection")
            return {"CANCELLED"}
        return {"FINISHED"}


class ExportSettings(bpy.types.PropertyGroup):
    export_dir: bpy.props.EnumProperty(
        name="Export Directory",
        items=[
            ("platinum/data/interiors_mbg/custom", "platinum/data/interiors_mbg/custom", "MBG textures"),
            ("platinum/data/interiors_mbp/custom", "platinum/data/interiors_mbp/custom", "MBP textures"),
            ("platinum/data/interiors_mbu/custom", "platinum/data/interiors_mbu/custom", "MBU textures"),
            ("platinum/data/interiors_pq/custom", "platinum/data/interiors_pq/custom", "PQ textures"),
            ("platinum/data/interiors/custom", "platinum/data/interiors/custom", ""),
            ("platinum/data/multiplayer/interiors/custom", "platinum/data/multiplayer/interiors/custom", ""),
            ("CUSTOM", "Custom Directory...", "")
        ],
        description="Where the interiors should be saved",
    )

    custom_export_dir: bpy.props.StringProperty(
        name="Custom Directory",
        default=""
    )


class EXPORT_PT_settings(bpy.types.Panel):
    bl_label = "AutoDIF Settings"
    bl_idname = "EXPORT_PT_settings"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "scene"

    def draw(self, context):
        layout = self.layout
        scene = context.scene
        settings = scene.autodif_export_settings

        layout.prop(settings, "export_dir")

        if settings.export_dir == "CUSTOM":
            layout.prop(settings, "custom_export_dir")


class DIFServer:
    _instance = None

    def __new__(cls, *args, **kwargs):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance

    def __init__(self, port=7654):
        if hasattr(self, "_initialized"):
            return
        self._initialized = True

        self.port = port
        self.server_thread = None
        self.stop_event = None
        self.current_conn = None
        self.difs_to_install = []

        self.log(f"Creating server")

    def __del__(self):
        self.stop()

    @property
    def prefs(self):
        return bpy.context.preferences.addons[__name__].preferences
    
    @property
    def interiors_relative_directory(self):
        if(bpy.context.scene.autodif_export_settings.export_dir == "CUSTOM"):
            return bpy.context.scene.autodif_export_settings.custom_export_dir
        else:
            return bpy.context.scene.autodif_export_settings.export_dir
    
    @property
    def interiors_directory(self):
        if self.interiors_relative_directory:
            return os.path.join(self.prefs.game_dir, *self.interiors_relative_directory.split("/"))
        else:
            return None

    def log(self, msg):
        now = datetime.datetime.now().strftime("%H:%M:%S.%f")
        print(f"[{now}] (AutoDIF) {msg}")

    def send_command(self, *args):
        """ Sends a BlenderConnection method with arguments for the connected game to execute """
        message = ""
        for arg in args:
            message += str(arg) + "|"

        # Format the message so torque can handle it as a string
        message = message.replace("\\", "\\\\")
        message = message.replace("\n", "\\n")
        message = message.replace('"','\\"')

        if self.current_conn:
            self.current_conn.sendall((message + "\n").encode())
            self.log(f"Sent message: {message}")

    def recieve_command(self, message):
        """ Processes a string sent from BlenderConnection, executing a DIFServer method with arguments """
        self.log(f"Got message: {message}")
        query = message.split("|")
        func = getattr(self, query[0])
        func(*query[1:])

    def install_difs(self):
        """ Moves the exported interior files into the game directory and tells the game to add them to the level """
        for dif_path in self.difs_to_install:
            interior_name = os.path.basename(dif_path)
            destination = os.path.join(self.interiors_directory, interior_name) 
            shutil.move(dif_path, destination)
            self.log(f"Moving {interior_name} to {destination}...")

        self.send_command("addNewInteriors")

    def export_difs(self):
        """ Exports the scene as DIF into a temp folder and tells the game to get ready for those files """

        # Verify attributes
        if not self.prefs.game_dir:
            raise Exception("Game directory not set. Check Preferences->Add-ons->Auto DIF.")
            
        if not self.interiors_relative_directory:
            raise Exception("Interior folder wasn't set. Check Scene->AutoDIF Settings->Export Directory.")
        
        # Export the difs into a temp location
        outfolder = os.path.join(tempfile.gettempdir(), "auto_dif")
        os.makedirs(outfolder, exist_ok=True)

        if bpy.data.filepath:
            difname, ext = os.path.splitext(os.path.basename(bpy.data.filepath))
        else:
            difname = "Untitled"

        outpath = os.path.join(outfolder, difname)
        self.log(f"Exporting difs to {outpath}...")
        bpy.ops.export_scene.dif(
            bspmode=self.prefs.bspmode,
            filepath=outpath
        )

        # Note the new files and tell the game to allocate them
        i = 0
        self.difs_to_install.clear()
        while True:
            file = outpath + str(i) + ".dif"
            if(not os.path.exists(file)):
                break
            self.difs_to_install.append(file)
            i += 1

        if i == 0:
            self.send_command("notifyError", "No difs were exported; perhaps the scene is empty or an error occured.")
        else:
            self.send_command(f"allocateDIFsPart1", self.interiors_relative_directory, difname, i)

    def handle_client(self):
        """ Called when a connection from the game is made """
        with self.current_conn:
            while True:
                try:
                    data = self.current_conn.recv(16000)
                except (ConnectionResetError, ConnectionAbortedError, OSError) as error:
                    self.log("Client connection closed")
                    return
                
                if not data:
                    self.log("Client disconnected")
                    return
                
                try:
                    self.recieve_command(data.decode())

                # Send errors to the game to show in a message box
                except Exception as error:
                    self.log(traceback.format_exc())
                    self.send_command("notifyError", f"{str(error)}")

    def server_loop(self):
        """ Listens for connections from the game """
        self.stop_event = threading.Event()
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.bind(("localhost", self.port))
            s.listen(1)
            s.settimeout(0.5)
            self.log(f"Server listening on port {self.port}")

            while not self.stop_event.is_set():
                try:
                    conn, addr = s.accept()
                except socket.timeout:
                    continue

                self.log(f"Got connection from {addr}")
                self.current_conn = conn
                self.handle_client()
            else:
                self.current_conn = None
                self.log("Server stopped")

    def start(self):
        if self.server_thread and self.server_thread.is_alive():
            self.stop()

        self.server_thread = threading.Thread(target=self.server_loop, daemon=True)
        self.server_thread.start()

    def stop(self):
        if self.current_conn:
            self.current_conn.shutdown(socket.SHUT_RDWR)
            self.current_conn.close()

        if self.stop_event:
            self.stop_event.set()

        if self.server_thread:
            self.server_thread.join(timeout=2.0)

        self.current_conn = None
        self.stop_event = None
        self.server_thread = None


def on_save(dummy):
    """ Called after the blend file is saved """
    if server.prefs.export_on_save:
        if(server.current_conn):
            server.export_difs()


def export_menu_func(self, context):
    self.layout.operator(EXPORT_OT_manual.bl_idname, text="To Marble Blast")


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.autodif_export_settings = bpy.props.PointerProperty(type=ExportSettings)

    bpy.types.TOPBAR_MT_file_export.append(export_menu_func)

    if on_save not in bpy.app.handlers.save_post:
        bpy.app.handlers.save_post.append(on_save)

    server.start()


def unregister():
    for cls in classes:
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.autodif_export_settings

    bpy.types.TOPBAR_MT_file_export.remove(export_menu_func)

    if on_save in bpy.app.handlers.save_post:
        bpy.app.handlers.save_post.remove(on_save)

    server.stop()


server = DIFServer()
classes = (ExportSettings, EXPORT_PT_settings, AutoDIFPreferences, EXPORT_OT_manual)