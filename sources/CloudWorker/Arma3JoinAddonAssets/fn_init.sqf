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

// Initial invalidation prevents a previous mission's successful sample being reused.
[0, 0] call UTFHC_fnc_emitJoin;
addMissionEventHandler ["OnUserConnected", { call UTFHC_fnc_sampleJoin; }];
addMissionEventHandler ["OnUserClientStateChanged", { call UTFHC_fnc_sampleJoin; }];
addMissionEventHandler ["OnUserDisconnected", { [0, 0] call UTFHC_fnc_emitJoin; }];
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
        uiSleep 2;
    };
};
uiNamespace setVariable ["UTFHC_joinLoop", _loop];
