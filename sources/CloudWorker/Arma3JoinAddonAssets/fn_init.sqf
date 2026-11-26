// This server-only observer never transfers AI or requires client-side code.
if (!isServer) exitWith {};

private _previous = uiNamespace getVariable ["UTFHC_joinLoop", scriptNull];
if (!isNull _previous) then { terminate _previous; };
missionNamespace setVariable ["UTFHC_joinActive", true];

UTFHC_fnc_emitJoin = {
    params ["_owner", "_state"];
    // Events run unscheduled; make the periodic caller's increment and write atomic as well.
    isNil {
        private _sequence = (uiNamespace getVariable ["UTFHC_joinSequence", 0]) + 1;
        uiNamespace setVariable ["UTFHC_joinSequence", _sequence];
        diag_log text format ["UTFHC|1|__UTF_LAUNCH_NONCE__|%1|%2|%3", _sequence toFixed 0, _owner toFixed 0, _state];
    };
};

UTFHC_fnc_sampleJoin = {
    if !(missionNamespace getVariable ["UTFHC_joinActive", false]) exitWith {};
    private _matches = [];
    {
        private _info = getUserInfo _x;
        if (count _info > 7) then {
            if ((_info select 7) isEqualTo true && {(_info select 3) isEqualTo "UTFHC1"}) then {
                _matches pushBack _info;
            };
        };
    } forEach allUsers;
    private _owner = 0;
    private _ready = 0;
    if (count _matches isEqualTo 1) then {
        private _info = _matches select 0;
        private _candidate = _info select 1;
        private _clientState = _info select 6;
        if (_clientState isEqualType "") then { _clientState = parseNumber _clientState; };
        if (_candidate isEqualType 0 && {_candidate > 2}) then {
            _owner = _candidate;
            if (_clientState isEqualTo 10) then { _ready = 1; };
        };
    };
    if (_ready isEqualTo 0) then { _owner = 0; };
    [_owner, _ready] call UTFHC_fnc_emitJoin;
};

// Counts connected human users and writes a nonce-bound UTFPL record. Headless clients (getUserInfo index 7)
// and the server itself (owner id 2 or lower) are never counted. A record is written when the count changes
// and at least every 30 seconds, so the diagnostic log does not grow by a line per sample.
UTFPL_fnc_samplePlayers = {
    if !(missionNamespace getVariable ["UTFHC_joinActive", false]) exitWith {};
    private _humans = 0;
    {
        private _info = getUserInfo _x;
        if (count _info > 7) then {
            private _owner = _info select 1;
            if (!((_info select 7) isEqualTo true) && {_owner isEqualType 0 && {_owner > 2}}) then {
                _humans = _humans + 1;
            };
        };
    } forEach allUsers;
    _humans = _humans min 1024;
    // Events run unscheduled; make the decision, the sequence increment and the write atomic as well.
    isNil {
        private _previousHumans = uiNamespace getVariable ["UTFPL_lastHumans", -1];
        private _lastEmission = uiNamespace getVariable ["UTFPL_lastEmission", -1000000];
        if (_humans != _previousHumans || {diag_tickTime - _lastEmission >= 30}) then {
            private _sequence = (uiNamespace getVariable ["UTFPL_playerSequence", 0]) + 1;
            uiNamespace setVariable ["UTFPL_playerSequence", _sequence];
            uiNamespace setVariable ["UTFPL_lastHumans", _humans];
            uiNamespace setVariable ["UTFPL_lastEmission", diag_tickTime];
            diag_log text format ["UTFPL|1|__UTF_LAUNCH_NONCE__|%1|%2", _sequence toFixed 0, _humans toFixed 0];
        };
    };
};

// Initial invalidation prevents a previous mission's successful sample being reused.
[0, 0] call UTFHC_fnc_emitJoin;
addMissionEventHandler ["OnUserConnected", { call UTFHC_fnc_sampleJoin; call UTFPL_fnc_samplePlayers; }];
addMissionEventHandler ["OnUserClientStateChanged", { call UTFHC_fnc_sampleJoin; call UTFPL_fnc_samplePlayers; }];
addMissionEventHandler ["OnUserDisconnected", { [0, 0] call UTFHC_fnc_emitJoin; call UTFPL_fnc_samplePlayers; }];
addMissionEventHandler ["Ended", {
    missionNamespace setVariable ["UTFHC_joinActive", false];
    [0, 0] call UTFHC_fnc_emitJoin;
}];
addMissionEventHandler ["MPEnded", {
    missionNamespace setVariable ["UTFHC_joinActive", false];
    [0, 0] call UTFHC_fnc_emitJoin;
}];

// Reconciliation also covers clients already connected before this mission initialized.
private _loop = [] spawn {
    while {missionNamespace getVariable ["UTFHC_joinActive", false]} do {
        call UTFHC_fnc_sampleJoin;
        call UTFPL_fnc_samplePlayers;
        uiSleep 2;
    };
};
uiNamespace setVariable ["UTFHC_joinLoop", _loop];
