// Basic idea:
// 1. Have Blender export DIF on save. Notify PQ.
// 2. Have PQ delete the related interiors and files. Notify Blender.
// 3. Have Blender move the new DIFs into the game files. Notify PQ.
// 4. Have PQ add the interiors back into the mission.

$BlenderPort = 7654;

function ConnectBlender() {
  if(isObject(MissionGroup)) {
    if(isObject(BlenderConnection)) {
      BlenderConnection.disconnect();
      BlenderConnection.delete();
    }
    %obj = new TCPObject(BlenderConnection);
    %obj.connect("127.0.0.1:" @ $BlenderPort);
    
    // Keep the connection confined to the current mission
    if(isObject(MissionCleanup))
      MissionCleanup.add(BlenderConnection);
  }
  else {
    error("Cannot connect with Blender outside of a level");
  }
}

function BlenderConnection::onConnectFailed(%this) {
  messageBoxOK("No connection", "Couldn't connect to Blender. Make sure you have the auto_dif plugin enabled."); //TODO add the link?
  %this.delete();
}

function BlenderConnection::onDisconnect(%this) {
  messageBoxOK("Blender Disconnected", "Blender was closed or an error occurred."); //TODO tell how to fix
  %this.delete();
}

function BlenderConnection::onConnected(%this) {
  echo("Connected to Blender; requesting scene");
  %this.sendCommand("export_difs");
}

function BlenderConnection::onLine(%this, %line) {
  echo("Recieved Blender message:" SPC %line);
  %this.recieveCommand(%line);
}

function BlenderConnection::sendCommand(%this, %name, %a1, %a2, %a3) {
  %message = %name;
  for(%i = 1; %a[%i] !$= ""; %i++) {
    %message = %message @ "|" @ %a[%i];
  }
  echo("Sending message:" SPC %message);
  %this.send(%message);
}

function BlenderConnection::recieveCommand(%this, %msg) {
  //%msg is in the format "methodName|arg1|arg2|arg3..."
  while(%msg !$= "") {
    %msg = nextToken(%msg, "token", "|");
    if(%func $= "") {
      %func = %this @ ".call(\"" @ %token @ "\"";
    }
    else {
      %func = %func @ "," SPC "\"" @ %token @ "\"";
    }
  }
  eval(%func @ ");");
}

function BlenderConnection::allocateDIFs(%this, %folderPath, %dif_name, %amt) { //TODO should we check the mission to see if the dif is in use currently?
  if(!isObject(MissionGroup)) {
    error("User is not in a mission");
    return;
  }
  
  // Unpause the game so we don't crash
  %this.pauseGame = $gamePaused;
  if(%this.pauseGame)
    $gamePaused = false;
  
  // Save the marble pos in case we fall through the interior
  %this.marbleTransform = LocalClientConnection.player.getTransform();
  
  // Clear the interior and subobjects
  while(isObject(BlenderInterior_g))
    BlenderInterior_g.delete();
  MissionGroup.add(new SimGroup(BlenderInterior_g));
  
  // Delete the old difs and allocate the new ones in the filesystem
  for(%i = 0; true; %i++) {
    %filePath = %folderPath @ "/" @ %dif_name @ %i @ ".dif";
    %needsCreate = (%i < %amt);
    %needsDelete = isFile(%filePath);
    
    if(!%needsCreate && !%needsDelete) {
      break;
    }
    if(%needsDelete) {
      for(%j = 0; %j < MissionGroup.getCount(); %j++) {
        %obj = MissionGroup.getObject(%j);
        if(%obj.interiorFile $= %filePath) {
          messageBoxOK("Error", %filePath SPC "is currently in use by another InteriorInstance! You'll have to delete it to continue a connection with Blender.");
          return;
        }
      }
      echo("Deleting" SPC %filePath);
      deleteFile(%filePath);
    }
    if(%needsCreate) {
      echo("Creating blank file" SPC %filePath);
      %touch = new FileObject();
      %touch.openForWrite(%filePath);
      %touch.close();
      %touch.delete();
      %this.newInteriors[%i] = %filePath;
    }
  }
  %this.newInteriorCount = %amt;
  
  // Actually install them
  %this.sendCommand("install_difs");
}

function BlenderConnection::addNewInteriors(%this) {
  if(!isObject(BlenderInterior_g)) {
    error("Blender interior group was not found");
    return;
  }
  
  // Add in the interiors using the cached filepaths
  for(%i = 0; %i < %this.newInteriorCount; %i++) {
    %obj = new InteriorInstance() {
      interiorFile = %this.newInteriors[%i];
      locked = true;
    };
    echo("Added new interior" SPC %obj);
    BlenderInterior_g.add(%obj);
    if(%i == 0)
      %obj.magicButton();
  }
  %this.newInteriorCount = 0;
  
  // Start the moving platforms
  for(%i = 0; %i < BlenderInterior_g.getCount(); %i++) {
    %group = BlenderInterior_g.getObject(%i);
    if(%group.getClassName() $= "SimGroup") {
      for(%j = 0; %j < %group.getCount(); %j++) {
        %mp = %group.getObject(%j);
        if(%mp.getClassName() $= "PathedInterior") {
          %mp.getDatablock().onMissionReset(%mp);
        }
      }
    }
  }
  
  // Put the marble back where it was in case it fell
  LocalClientConnection.player.setTransform(%this.marbleTransform);
  
  // Repause if necessary
  $gamePaused = %this.pauseGame;
}

function BlenderConnection::notifyError(%this, %message) {
  messageBoxOK("Error", "From Blender:" SPC %message);
}