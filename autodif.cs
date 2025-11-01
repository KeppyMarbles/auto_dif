// Created by Keppy

$ConstructorPort = 7653;

package AutoDIF {
  function Plugin::Activate(%this, %version, %inst, %static) {
    if(%version != 1) {
      return tool.FUNC_BADVERSION();
    }
    
    if(scene.getCurrentFile() $= scene.getCurrentName()) {
      tool.activateErrorMsg = "This plugin requires the scene to be saved.";
      return tool.FUNC_BADGENERAL();
    }
    
    if(!%static.init) {
      AutoDIF_InitStatic(%static);
    }
    
    if(isObject(MBConnection)) {
      MBConnection.disconnect();
      MBConnection.delete();
    }
    new TCPObject(MBConnection);
    
    
    MBConnection.listen($ConstructorPort);
    
    %plugin = new ScriptObject();
    %plugin.static = %static;
    MBConnection.static = %static;
    
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
    %form.addFieldListItem(0, %inst.instance.static.folders[1]);
    %form.addFieldListItem(0, %inst.instance.static.folders[2]);
    %form.addFieldListItem(0, %inst.instance.static.folders[3]);
    %form.addFieldListItem(0, %inst.instance.static.folders[4]);
    %form.addFieldListItem(0, %inst.instance.static.folders[5]);
    %form.addFieldListItem(0, %inst.instance.static.folders[6]);
    %form.addField(1, "Export On Save", "checkbox");
    %form.addField(2, "Build BSP", "checkbox");
    inputPopCursor();
  }
  
  function Plugin::InterfaceGet(%this, %inst, %id) {
    switch(%id) {
      case 0:
        return $pref::AutoDIF::InteriorsFolder_[scene.getCurrentName()];
      case 1:
        return $pref::AutoDIF::ExportOnSave;
      case 2:
        return $pref::AutoDIF::BuildBSP;
    }
  }
  
  function Plugin::InterfaceSet(%this, %inst, %id, %value) {
    switch(%id) {
      case 0:
        $pref::AutoDIF::InteriorsFolder_[scene.getCurrentName()] = %value;
      case 1: 
        $pref::AutoDIF::ExportOnSave = %value;
      case 3:
        $pref::AutoDIF::BuildBSP = %value;
    }
  }
  
  function AutoDIF_InitStatic(%static) {
    %static.folders[1] = "platinum/data/interiors_mbg/custom";
    %static.folders[2] = "platinum/data/interiors_mbp/custom";
    %static.folders[3] = "platinum/data/interiors_mbu/custom";
    %static.folders[4] = "platinum/data/interiors_pq/custom";
    %static.folders[5] = "platinum/data/interiors/custom";
    %static.folders[6] = "platinum/data/multiplayer/interiors/custom";
    
    %static.init = true;
  }
};

package ExportOnSave {
  function CSceneManager::save(%this) {
    %needsSave = scene.getCurrent().isModified();
    Parent::save(%this);
    if($pref::AutoDIF::ExportOnSave) {
      if(isObject(MBConnectionClient) && %needsSave)
        MBConnectionClient.startAllocate();
    }
  }
};
activatePackage(ExportOnSave);

package csx3difPipe {
  function onAsyncPipeText(%pipe, %line) {
    echo(%line);
  }
  function onAsyncPipeEOF(%pipe) {
    forceBackgroundSleep(0);
    %pipe.schedule(32, "delete");
    MBConnectionClient.copyDif();
    deactivatePackage(csx3difPipe);
  }
};

function CSceneManager::getCurrentFile(%this) {
  return %this.getCurrent().getName();
}

function CSceneManager::getCurrentName(%this) {
  return fileBase(%this.getCurrentFile());
}

function CSceneManager::findInvalidMP(%this) {
  %currentMap = %this.getCurrentMap();
  %doors = 0;
  for(%i = 0; %i < %currentMap.getNumEntities(); %i++) {
    %id = %currentMap.getEntityID(%i);
    %classname = %currentMap.getEntityClassname(%id);
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

function MBConnectionClient::onDisconnect(%this) {
  %this.delete();
}

function MBConnectionClient::onLine(%this, %line) {
  echo("Recieved Marble Blast message:" SPC %line);
  %this.recieveCommand(%line);
}

function MBConnection::onConnectRequest(%this, %ip, %id) {
  echo("Got request");
  if(isObject(MBConnectionClient)) {
    MBConnectionClient.disconnect();
    MBConnectionClient.delete();
  }
  new TCPObject(MBConnectionClient, %id);
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

function MBConnectionClient::getDifPath(%this) {
  %id = $pref::AutoDIF::InteriorsFolder_[scene.getCurrentName()];
  %folder = MBConnection.static.folders[%id];
  return %folder @ "/" @ scene.getCurrentName() @ ".dif";
}

function MBConnectionClient::startAllocate(%this) {
  // Make sure everything is ready
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
  if($pref::AutoDIF::InteriorsFolder_[scene.getCurrentName()] $= "") {
    %this.sendCommand("notifyError", "Interiors folder not set");
    return;
  }
  if(isActivePackage(csx3difPipe)) {
    error("AutoDIF: An export is already in progress; skipping");
    return;
  }
  
  // Validate moving platforms
  %mp = scene.findInvalidMP();
  if(%mp != -1) {
    %this.sendCommand("notifyError", "There is a Door_Elevator (entity id" SPC %mp SPC ") that does not have enough path nodes.");
    return;
  }
  
  // All ready to go... hopefully
  %this.sendCommand("allocateDIFsPart1", %this.getDifPath());
}

function MBConnectionClient::exportDif(%this, %game_exe_directory) {
  %this.game_exe_directory = %game_exe_directory;
  
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

function MBConnectionClient::copyDif(%this) {
  %adjDif = strreplace(scene.getCurrentFile(), ".csx", ".dif");
  %targetDifDir = filePath(%this.game_exe_directory) @ "/" @ %this.getDifPath();
  
  echo("Copying" SPC %adjDif SPC "to" SPC %targetDifDir);
  %result = pathCopy(%adjDif, %targetDifDir, 0);
  
  if(!%result) {
    %this.sendCommand("notifyError", "The DIF didn't get copied. Perhaps it failed to export.");
    return;
  }
  
  if(fileDelete(%adjDif)) {
    echo("Deleted file" SPC %adjDif);
  }

  %this.sendCommand("addNewInteriors");
}

tool.register("AutoDIF", tool.typeDialog(), tool.RFLAG_NONE(), "AutoDIF" );

tool.setToolProperty("AutoDIF", "Icon", "standardicons/default");
tool.setToolProperty("AutoDIF", "Group", "Keppy's Plugins");

function re() {
	exec($Con::File);
}