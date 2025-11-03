// Created by Keppy

// ---------------- Plugin ----------------

package AutoDIF {
  function Plugin::Activate(%this, %version, %inst, %static) {
    if(%version != 1) {
      return tool.FUNC_BADVERSION();
    }
    
    %plugin = new ScriptObject();
    %plugin.static = %static;
    
    %plugin.dirty = tool.DIRTY_NONE();
    %plugin.active = true;
    %plugin.update = tool.EDIT_DONOTHING();

    %inst.instance = %plugin;
    %inst.flagsInterface = tool.IFLAG_NOTOOLCURSOR();

    return tool.FUNC_OK();
  }
  
  function Plugin::Interface(%this, %inst, %form) {
    %form.defineTitle("Auto DIF");
    %form.addField(0, "Interior Folder", "popup");
    %form.addFieldListItem(0, "");
    %form.addFieldListItem(0, MBConnection.folders[1]);
    %form.addFieldListItem(0, MBConnection.folders[2]);
    %form.addFieldListItem(0, MBConnection.folders[3]);
    %form.addFieldListItem(0, MBConnection.folders[4]);
    %form.addFieldListItem(0, MBConnection.folders[5]);
    %form.addFieldListItem(0, MBConnection.folders[6]);
    %form.addField(1, "Export On Save", "checkbox");
    %form.addField(2, "Build BSP", "checkbox");
    inputPopCursor();
  }
  
  function Plugin::InterfaceGet(%this, %inst, %id) {
    switch(%id) {
      case 0:
        return scene.getInteriorsFolderID();
      case 1:
        return $pref::AutoDIF::ExportOnSave;
      case 2:
        return $pref::AutoDIF::BuildBSP;
    }
  }
  
  function Plugin::InterfaceSet(%this, %inst, %id, %value) {
    switch(%id) {
      case 0:
        scene.setInteriorsFolderID(%value);
      case 1:
        $pref::AutoDIF::ExportOnSave = %value;
      case 2:
        $pref::AutoDIF::BuildBSP = %value;
    }
  }
};

// ---------------- Packages ----------------

package AutoDIFSave {
  function CSceneManager::save(%this) {
    %this.applyEntityRotations();
    %needsSave = scene.getCurrent().isModified();
    
    Parent::save(%this);
    
    if($pref::AutoDIF::ExportOnSave) {
      if(isObject(MBConnectionClient) && %needsSave)
        MBConnectionClient.export_difs();
    }
  }
};

package csx3difPipe {
  function onAsyncPipeText(%pipe, %line) {
    echo(%line);
  }
  function onAsyncPipeEOF(%pipe) {
    forceBackgroundSleep(0);
    %pipe.schedule(32, "delete");
    MBConnectionClient.findDIFs();
    deactivatePackage(csx3difPipe);
  }
};

// ---------------- Helpers ----------------

function InteriorMap::getEntityPropertyByName(%this, %id, %name) {
  for(%i = 0; %i < %this.getEntityNumProperties(%id); %i++) {
    %prop = %this.getEntityProperty(%id, %i);
    if(firstWord(%prop) $= %name) {
      return getWord(%prop, 1);
    }
  }
  return "";
}

function InteriorMap::findInvalidMP(%this) {
  // Find a Door_Elevator which has less than 2 path nodes
  %doors = 0;
  for(%i = 0; %i < %this.getNumEntities(); %i++) {
    %id = %this.getEntityID(%i);
    %classname = %this.getEntityClassname(%id);
    if(%classname $= "Door_Elevator") {
      %current_door = %doors;
      %ids[%current_door] = %id;
      %doors += 1;
    }
    else if(%classname $= "path_node") {
      %path_nodes[%current_door] += 1;
    }
  }
  for(%i = 0; %i < %doors; %i++) {
    if(%path_nodes[%i] < 2)
      return %ids[%i];
  }
  return -1;
}

function CSceneManager::getCurrentFile(%this) {
  return %this.getCurrent().getName();
}

function CSceneManager::getCurrentName(%this) {
  return fileBase(%this.getCurrentFile());
}

function CSceneManager::getInteriorsFolderID(%this) {
  return %this.getCurrentMap().getEntityPropertyByName(0, "interiorsFolder");
}

function CSceneManager::setInteriorsFolderID(%this, %id) {
  %this.getCurrentMap().addEntityProperty(0, "interiorsFolder", %id);
}

function CSceneManager::applyEntityRotations(%this) {
  // Set the rotation property on the entity itself so it can be applied in-game
  %currentMap = %this.getCurrentMap();
  %currentScene = %this.getCurrent();
  for(%i = 0; %i < %currentMap.getNumEntities(); %i++) {
    %entityID = %currentMap.getEntityID(%i);
    %shapeID = %currentScene.getPointEntityShapeID(%currentMap, %entityID);
    if(%shapeID != -1) {
      %shape = %currentScene.getShapeSimObjectID(%shapeID);
      %currentMap.addEntityProperty(%entityID, "rotation", %shape.rotation);
    }
  }
}

// ---------------- Connection ----------------

function MBConnection::onConnectRequest(%this, %ip, %id) {
  echo("Got request");
  if(isObject(MBConnectionClient)) {
    MBConnectionClient.disconnect();
    MBConnectionClient.delete();
  }
  new TCPObject(MBConnectionClient, %id);
}

function MBConnectionClient::onDisconnect(%this) {
  %this.delete();
}

function MBConnectionClient::onLine(%this, %line) {
  echo("Recieved Marble Blast message:" SPC %line);
  %this.recieveCommand(%line);
}

function MBConnectionClient::sendCommand(%this, %name, %a1, %a2, %a3) {
  %message = %name @ "|" @ %a1 @ "|" @ %a2 @ "|" @ %a3;
  echo("Sending message:" SPC %message);
  %this.send(%message @ "\n");
}

function MBConnectionClient::recieveCommand(%this, %msg) {
  //%msg is in the format "methodName|arg1|arg2|arg3..."
  while(%msg !$= "") {
    %msg = nextToken(%msg, "token", "|");
    if(%func $= "")
      %func = %this @ "." @ %token @ "(";
    else {
      %func = %func @ "\"" @ %token @ "\"";
      if(%msg !$= "")
        %func = %func @ ",";
    }
  }
  eval(%func @ ");");
}

function MBConnectionClient::export_difs(%this) {
  // Make sure everything is ready
  if(isActivePackage(csx3difPipe)) {
    error("AutoDIF: An export is already in progress; skipping");
    return;
  }
  if(!isFile("csx3dif.exe")) {
    %this.sendCommand("notifyError", "csx3dif.exe was not found in Constructor root directory.");
    return;
  }
  %map = scene.getCurrentMap();
  if(!isObject(%map) || %map.getNumEntityChildren(0) == 0) {
    %this.sendCommand("notifyError", "Worldspawn is empty");
    return;
  }
  if(scene.getCurrentFile() $= scene.getCurrentName()) {
    %this.sendCommand("notifyError", "Scene needs to be saved to a file");
    return;
  }
  if(scene.getInteriorsFolderID() $= "") {
    %this.sendCommand("notifyError", "Interiors folder not set");
    return;
  }
  
  // Validate moving platforms
  %mp = %map.findInvalidMP();
  if(%mp != -1) {
    %this.sendCommand("notifyError", "There is a Door_Elevator (entity id" SPC %mp @ ") that does not have enough path nodes.");
    return;
  }
  
  // All ready to go... hopefully
  %args = scene.getCurrentFile();
  if(!$pref::AutoDIF::BuildBSP) {
    %args = %args SPC "--bsp none";
  }

  activatePackage(csx3difPipe);
  %result = executeAndLog("csx3dif" SPC %args);
  if(!%result) {
    %this.sendCommand("notifyError", "There was a problem when trying to execute csx3dif.");
    deactivatePackage(csx3difPipe);
    return;
  }
}

function MBConnectionClient::findDIFs(%this) {
  %dif_dir = filePath(scene.getCurrentFile());
  %i = 0;
  
  while(true) {
    if(%i == 0)
      %path = %dif_dir @ "/" @ scene.getCurrentName() @ ".dif";
    else
      %path = %dif_dir @ "/" @ scene.getCurrentName() @ "-" @ %i @ ".dif";
    
    if(!PlatformIsFile(%path))
      break;
    
    %this.interiorsToInstall[%i] = %path;
    %i++;
  }
  %this.newInteriorCount = %i;
  
  if(%i == 0)
    %this.sendCommand("notifyError", "No difs were exported; perhaps an error occured in csx3dif.");
  else
    %this.sendCommand("allocateDIFsPart1", MBConnection.folders[scene.getInteriorsFolderID()], scene.getCurrentName(), %i);
}

function MBConnectionClient::install_difs(%this, %game_exe_directory) {
  %interiors_directory = filePath(%game_exe_directory) @ "/" @ MBConnection.folders[scene.getInteriorsFolderID()];
  
  for(%i = 0; %i < %this.newInteriorCount; %i++) {
    %difPath = %this.interiorsToInstall[%i];
    %newDifPath = %interiors_directory @ "/" @ fileName(%difPath);
    echo("Copying" SPC %difPath SPC "to" SPC %newDifPath);
    %result = pathCopy(%difPath, %newDifPath, false);
    if(!%result) {
      %this.sendCommand("notifyError", "There was a problem trying to copy" SPC %difPath SPC "to" SPC %newDifPath @ ".");
      return;
    }
    if(fileDelete(%difPath)) {
      echo("Deleted file" SPC %difPath);
    }
  }
  
  %this.sendCommand("addNewInteriors");
}

// ---------------- Initialization ----------------

if(isObject(MBConnection)) {
  MBConnection.disconnect();
  MBConnection.delete();
}
new TCPObject(MBConnection);

MBConnection.folders[1] = "platinum/data/interiors_mbg/custom";
MBConnection.folders[2] = "platinum/data/interiors_mbp/custom";
MBConnection.folders[3] = "platinum/data/interiors_mbu/custom";
MBConnection.folders[4] = "platinum/data/interiors_pq/custom";
MBConnection.folders[5] = "platinum/data/interiors/custom";
MBConnection.folders[6] = "platinum/data/multiplayer/interiors/custom";

MBConnection.listen(7653);

if($pref::AutoDIF::ExportOnSave $= "")
  $pref::AutoDIF::ExportOnSave = 1;
if($pref::AutoDIF::BuildBSP $= "")
  $pref::AutoDIF::BuildBSP = 0;

activatePackage(AutoDIFSave);

tool.register("AutoDIF", tool.typeDialog(), tool.RFLAG_NONE(), "AutoDIF" );

tool.setToolProperty("AutoDIF", "Icon", "standardicons/default");
tool.setToolProperty("AutoDIF", "Group", "Keppy's Plugins");