[Code]
//Puts a version number into an array
function getVersionNumberArray(versionNumber:string; firstDelimiter:char):TIntegerArray;
var
  positionCurrent, counter: integer;
  versionNumberArray: TIntegerArray;
  digitVersionNumber: string;
begin
    positionCurrent:= 1;

  //Put version number in array
  while not (positionCurrent = 0) do
  begin
    digitVersionNumber:= versionNumber;

    //set array length dynamically
    setArrayLength(versionNumberArray, (counter+1));

    //Determine position of first delimiter
    positionCurrent := pos(firstDelimiter,versionNumber);

    //..and delete the following characters to get the number
    delete(digitVersionNumber, positionCurrent,length(versionNumber));

    //Remaining version number without prior number
    delete(versionNumber, 1, positionCurrent);

    versionNumberArray[counter]:= strToIntDef(trim(digitVersionNumber),-1);
    counter := counter + 1;
  end;
  result:= versionNumberArray;
end;


// Compares the version in the string with a version number
// Parameters:
// VersionString: Version as a string to be evaluated
// Major, Minor, Patch, Build, Pre: Version to be compared with
// Delimiter: Character delimiter in the string for subversions (e.g., '.' for '1.2.3.4')
// Space (' ') or #0 is replaced by '.'
// Return:
// -1 if the new version number is smaller
// 0 if the numbers are equal
// 1 if the new version number is greater
// -256 for empty or invalid version string
// Additionally, the type of version change is stored in the variable 'kindOfVersionChange' and
// can be read after calling the function
// Examples:
//  compareVersions('7u10', '7u9', 'u', '')
//  compareVersions('6.5.3', '6.6.0-beta', '.', '-')
//  compareVersions('2.0-beta.2', '2.0-beta.1.0.1', '.', '-')
function compareVersions(fullVersionNumberNew: string; fullVersionNumberCurrent:string; firstDelimiter:char; secondDelimiter: char): integer;
var
  positionCurrent, positionNew, i, j, k, counter, arrayLength, versionArrayLength: integer;
  tempCurrentVersion, tempNewVersion: string;
  //pre-release states, e.g.: alpha, beta, rc
  preReleaseStateCurrent: string;
  preReleaseStateNew: string;
  preReleaseStateCurrentInt: integer;
  preReleaseStateNewInt: integer;
  //pre-release number e.g.: 1.0, 2.01, 3
  preReleaseNumberCurrent, preReleaseNumberNew: string;
  preReleaseArrCurrent, preReleaseArrNew: array of integer;
  //pre-release suffix, e.g.: beta.1, rc.2.0
  suffixCurrentVersion: string;
  suffixNewVersion: string;
  kindOfVersionChange: string;
  versionArrNew:array of integer;
  versionArrCurrent:array of integer;
  preReleaseStates: TArrayOfString;
begin
  counter:= 0;
  result:= 0;
  kindOfVersionChange:= 'none';
  suffixCurrentVersion:= 'none';
  suffixNewVersion:= 'none';
  preReleaseStates:= ['none', 'snapshot', 'alpha', 'beta', 'rc'];
  //Initialise arrays

  log('***Enter compareVersions()***');

  tempCurrentVersion:= fullVersionNumberCurrent;
  tempNewVersion:= fullVersionNumberNew;
  positionCurrent:= pos(secondDelimiter, tempCurrentVersion);

  if not(pos(secondDelimiter, tempCurrentVersion) = 0) then
  begin
    //get pre-release suffix, e.g. beta.1
    suffixCurrentVersion:= copy(tempCurrentVersion, positionCurrent+1,length(tempCurrentVersion));
    preReleaseStateCurrent:= suffixCurrentVersion;
    //get pre-release state, e.g. beta
    positionCurrent:= pos(firstDelimiter,suffixCurrentVersion);
    preReleaseStateCurrent:= trim(copy(suffixCurrentVersion, 0, positionCurrent-1));
    //get pre-release number, e.g. 1
    preReleaseNumberCurrent:= copy(suffixCurrentVersion, positionCurrent+1, length(suffixCurrentVersion));
    //get version number, e.g. 2.1
    tempCurrentVersion:= copy(tempCurrentVersion, 0,  pos(secondDelimiter, tempCurrentVersion)-1);
  end;

  positionNew := pos(secondDelimiter,tempNewVersion);

  if not(pos(secondDelimiter,tempNewVersion) = 0) then
  begin
    //get pre-release suffix, e.g. beta.1
    suffixNewVersion:= copy(tempNewVersion, positionNew+1,length(tempNewVersion));
    preReleaseStateNew:= suffixNewVersion;
    //get pre-release state, e.g. beta
    positionNew:= pos(firstDelimiter,suffixNewVersion);
    preReleaseStateNew:= trim(copy(suffixNewVersion, 0, positionNew-1));
    //get pre-release number, e.g. 1; strToIntDef returns 0 if convert fails
    preReleaseNumberNew:= copy(suffixNewVersion, positionNew+1, length(suffixNewVersion));
    tempNewVersion:= copy(tempNewVersion, 0,  pos(secondDelimiter,tempNewVersion)-1);
  end;

  //Save version number and pre release number as array
  versionArrCurrent:= getVersionNumberArray(tempCurrentVersion, firstDelimiter);
  versionArrNew:= getVersionNumberArray(tempNewVersion, firstDelimiter);
  preReleaseArrCurrent:= getVersionNumberArray(preReleaseNumberCurrent, firstDelimiter);
  preReleaseArrNew:= getVersionNumberArray(preReleaseNumberNew, firstDelimiter);

  //Check if current or new version array is longer to avoid array overflow in for-loop
  if (getArrayLength(versionArrNew) > getArrayLength(versionArrCurrent)) then
  begin
    for i:=getArrayLength(versionArrCurrent) to getArrayLength(versionArrNew) do
    begin
      setArrayLength(versionArrCurrent, i+1);
      versionArrCurrent[i]:= 0;
      versionArrayLength:= i;
    end;
  end
  else if (getArrayLength(versionArrNew) < getArrayLength(versionArrCurrent)) then
  begin
    for i:=getArrayLength(versionArrNew) to (getArrayLength(versionArrCurrent)-1) do
    begin
      setArrayLength(versionArrNew, i+1);
      versionArrNew[i]:= 0;
      versionArrayLength:= i;
    end;
  end
  else
  begin
    //Arrays are same length
    versionArrayLength:= getArrayLength(versionArrNew);
  end;

  for i:=0 to versionArrayLength do
  begin
    if(versionArrNew[i] < versionArrCurrent[i]) then
    begin
      //log('New version number ' +fullVersionNumberNew+ ' is smaller than ' +fullVersionNumberCurrent);
      result:= -1;
      break;
    end

    //If digits are equal
    else if(versionArrNew[i] = versionArrCurrent[i]) then
    begin
      //If last digits of version numbers are compared to each other
      if (i = (getArrayLength(versionArrNew)-1)) then
      begin
        //If version has a suffix
        if not(suffixCurrentVersion = 'none')then
        begin
          //result:= 0;
          for j:=0 to (getArrayLength(preReleaseStates)-1) do
          begin
            if (preReleaseStates[j] = preReleaseStateCurrent) then
            begin
              preReleaseStateCurrentInt:= j;
            end;

            if (preReleaseStates[j] = preReleaseStateNew) then
            begin
              preReleaseStateNewInt:= j;
            end;
          end;

          if (preReleaseStateCurrentInt = preReleaseStateNewInt) then
          begin
            //Determine pre-release array length for loop. Always save the smaller array size to avoid array overflow
            if (getArrayLength(preReleaseArrNew) < getArrayLength(preReleaseArrCurrent)) then
            begin
              arrayLength:= getArrayLength(preReleaseArrNew);
            end
            else
            begin
              arrayLength:= getArrayLength(preReleaseArrCurrent);
            end;
            //Compare pre-release numbers
            for k:=0 to (arrayLength-1) do
            begin
              if(preReleaseArrNew[k] < preReleaseArrCurrent[k]) then
              begin
                //log('New pre-release number ' +fullVersionNumberNew+ ' is smaller than ' +fullVersionNumberCurrent);
                result:= -1;
                break;
              end
              else if(preReleaseArrNew[k] = preReleaseArrCurrent[k]) then
              begin
                //Compare last digit of pre release numbers
                if (k = (arrayLength-1)) then
                begin
                  if (getArrayLength(preReleaseArrNew) > getArrayLength(preReleaseArrCurrent)) then
                  begin
                    //log('New pre-release number ' +fullVersionNumberNew+ ' is greater than ' +fullVersionNumberCurrent);
                    result:= 1
                    kindOfVersionChange:= 'pre';
                  end
                  else if (getArrayLength(preReleaseArrNew) < getArrayLength(preReleaseArrCurrent)) then
                  begin
                    //log('New pre-release number ' +fullVersionNumberNew+ ' is smaller than ' +fullVersionNumberCurrent);
                    result:= -1;
                  end
                  else
                  begin
                    //log('New pre-release number ' +fullVersionNumberNew+ ' is equal to ' +fullVersionNumberCurrent);
                    result:= 0;
                    break;
                  end;
                end;
              end
              else if(preReleaseArrNew[k] > preReleaseArrCurrent[k]) then
              begin
                //log('New pre-release number ' +fullVersionNumberNew+ ' is greater than ' +fullVersionNumberCurrent);
                kindOfVersionChange:= 'pre';
                result:= 1;
                break;
              end;
            end;
          end
          else if (preReleaseStateNewInt > preReleaseStateCurrentInt) then
          begin
            //log('New version number ' +fullVersionNumberNew+ ' is greater than ' +fullVersionNumberCurrent);
            kindOfVersionChange:= 'pre';
            result:= 1;
            break;
          end
          else if (preReleaseStateNewInt < preReleaseStateCurrentInt) then
          begin
            //0= no pre-release number
            //new release number is without pre state
            if (preReleaseStateNewInt = 0) then
            begin
              kindOfVersionChange:= 'pre';
              result:= 1;
              break;
            end
            else
            begin
              //log('New version number ' +fullVersionNumberNew+ ' is smaller than ' +fullVersionNumberCurrent);
              result:= -1;
              break;
            end;
          end;
          //log('New version number ' +fullVersionNumberNew+ ' is equal to ' +fullVersionNumberCurrent);
        end;
        break;
      end;
    end

    else if(versionArrNew[i] > versionArrCurrent[i]) then
    begin
      case i of
      0:begin
          kindOfVersionChange:= 'major';

          if (versionArrCurrent[i]=0) then
            kindOfVersionChange:= 'new';
        end;
      1: kindOfVersionChange:= 'minor';
      2: kindOfVersionChange:= 'patch';
      3: kindOfVersionChange:= 'build';
      end;
      //log('New version number ' +fullVersionNumberNew+ ' is greater than ' +fullVersionNumberCurrent);
      result:= 1;
      break;
    end;
  end;
  log('New version number: ' +fullVersionNumberNew+ ' OldVersion: ' +fullVersionNumberCurrent+ ' Change: ' +kindOfVersionChange+ ' Result: ' +intToStr(result));
  log('!!*Leave compareVersions()*!!');
end;